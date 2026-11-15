using System.Data;
using System.Text.Json;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;

namespace ASI.Basecode.Services.Services
{
    public sealed class UserService : ServiceBase, IUserService
    {
        private readonly IUserRepository _accountRepository;
        private readonly IAuditRepository _auditRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWorkflowLockManager _lockManager;
        private readonly UserManager<User> _userManager;
        private static readonly string[] PrimaryRoles = [RoleNames.Borrower, RoleNames.Custodian, RoleNames.Administrator];

        public UserService(
            IUserRepository accounts,
            IAuditRepository audits,
            IUnitOfWork work,
            IWorkflowLockManager locks,
            UserManager<User> users
        )
        {
            _accountRepository = accounts;
            _auditRepository = audits;
            _unitOfWork = work;
            _lockManager = locks;
            _userManager = users;
        }

        public async Task<AdminUsersPage> GetPageAsync(
            string? q,
            string? role,
            string? status,
            int page = 1,
            CancellationToken cancellationToken = default
        )
        {
            q = SearchText(q);
            role = PrimaryRoles.Contains(role) ? role! : "all";
            status = status is "active" or "inactive" or "pending" or "rejected" ? status : "all";
            var data = await _accountRepository.ListAsync(q, role, status, page, cancellationToken);
            var accountRows = data.Accounts;
            var roleRows = data.Roles;
            var total = data.Total;
            var pendingTotal = data.Pending;
            var pages = data.Pages;
            page = data.Page;
            var rows = accountRows
                .Select(x => new AdminUserRow(
                    x.Id,
                    x.FullName,
                    x.Email ?? string.Empty,
                    string.Join(", ", roleRows.Where(r => r.UserId == x.Id).Select(r => r.Name).Order()),
                    x.IsActive,
                    x.ApprovalStatus,
                    x.BorrowerProfile?.StudentNumber,
                    x.BorrowerProfile?.Department,
                    x.BorrowerProfile?.IsEligible,
                    ManilaTime.Format(x.CreatedAtUtc)
                ))
                .ToList();
            return new AdminUsersPage(rows, q, role, status, page, pages, total, pendingTotal, null, null);
        }

        public async Task<OperationResult<string>> DecideAsync(
            RequestContext actor,
            string id,
            AccountApprovalStatus decision,
            CancellationToken cancellationToken
        )
        {
            var target = await _accountRepository.FindAsync(id, cancellationToken);
            if (target is null)
            {
                return OperationResult<string>.Missing();
            }

            // Pending registrations can be approved or rejected; a rejected registration can still be approved later.
            var allowed =
                target.ApprovalStatus == AccountApprovalStatus.PendingApproval
                || decision == AccountApprovalStatus.Approved && target.ApprovalStatus == AccountApprovalStatus.Rejected;
            if (!allowed)
            {
                return OperationResult<string>.Invalid($"{target.FullName}'s registration has already been decided.");
            }

            var before = JsonSerializer.Serialize(new { target.ApprovalStatus, target.EmailConfirmed });
            target.ApprovalStatus = decision;
            // Administrator approval replaces sign-up email verification.
            if (decision == AccountApprovalStatus.Approved)
            {
                target.EmailConfirmed = true;
            }

            target.UpdatedAtUtc = DateTime.UtcNow;
            var updated = await _userManager.UpdateAsync(target);
            if (!updated.Succeeded)
            {
                return OperationResult<string>.Invalid(Errors.StaleAccountPage);
            }

            var approved = decision == AccountApprovalStatus.Approved;
            _auditRepository.Add(
                new AuditEvent
                {
                    ActorUserId = actor.UserId,
                    Action = approved ? "Admin.RegistrationApproved" : "Admin.RegistrationRejected",
                    EntityType = "User",
                    EntityId = target.Id,
                    Outcome = "Succeeded",
                    BeforeJson = before,
                    AfterJson = JsonSerializer.Serialize(new { target.ApprovalStatus, target.EmailConfirmed }),
                    IpAddress = actor.IpAddress,
                    CorrelationId = actor.CorrelationId,
                }
            );
            await _unitOfWork.SaveAsync(cancellationToken);
            return OperationResult<string>.Ok(
                id,
                approved ? $"{target.FullName}'s account was approved." : $"{target.FullName}'s registration was rejected."
            );
        }

        public async Task<OperationResult<string>> EditAsync(
            RequestContext actor,
            string id,
            UserEditModel input,
            CancellationToken cancellationToken
        )
        {
            var errors = new List<FieldError>();
            if (!PrimaryRoles.Contains(input.Role))
            {
                errors.Add(new(nameof(input.Role), Errors.InvalidRole));
            }

            if (errors.Count > 0)
            {
                return new(OperationOutcome.Invalid, Errors: errors);
            }

            try
            {
                await using var transaction = await _unitOfWork.BeginAsync(IsolationLevel.ReadCommitted, cancellationToken);
                await AcquireLockAsync("CampusGear:UserAdministration", cancellationToken);
                var actorId = actor.UserId;
                var actorAccount = actorId is null ? null : await _accountRepository.FindFreshAsync(actorId, cancellationToken);
                if (
                    actorAccount is null
                    || !actorAccount.IsActive
                    || !actorAccount.EmailConfirmed
                    || !await _userManager.IsInRoleAsync(actorAccount, RoleNames.Administrator)
                )
                {
                    return new(OperationOutcome.Forbidden);
                }

                var target = await _accountRepository.FindWithProfileAsync(id, cancellationToken);
                if (target is null)
                {
                    return OperationResult<string>.Missing();
                }

                var profile = target.BorrowerProfile;
                if (profile is not null)
                {
                    await AcquireLockAsync($"CampusGear:BorrowerProfile:{profile.Id:N}", cancellationToken);
                    await _accountRepository.ReloadProfileAsync(profile, cancellationToken);
                }

                if (
                    !string.Equals(input.ConcurrencyStamp, target.ConcurrencyStamp, StringComparison.Ordinal)
                    || !SameRowVersion(profile?.RowVersion, input.ProfileRowVersion)
                )
                {
                    throw new AdminUserEditException(Errors.StaleAccountEdit, true);
                }

                var currentRoles = await _userManager.GetRolesAsync(target);
                var roleChanged = currentRoles.Count != 1 || currentRoles[0] != input.Role;
                if (target.Id == actorId && (!input.IsActive || input.Role != RoleNames.Administrator))
                {
                    throw new AdminUserEditException(Errors.SelfAccessChangeForbidden);
                }

                if (input.Role != RoleNames.Borrower && target.ApprovalStatus != AccountApprovalStatus.Approved)
                {
                    throw new AdminUserEditException(Errors.StaffApprovalRequired);
                }

                if (
                    target.IsActive
                    && currentRoles.Contains(RoleNames.Administrator)
                    && (!input.IsActive || input.Role != RoleNames.Administrator)
                )
                {
                    var otherAdministrator = await _accountRepository.HasOtherAdministratorAsync(target.Id, cancellationToken);
                    if (!otherAdministrator)
                    {
                        throw new AdminUserEditException(Errors.LastAdministratorRequired);
                    }
                }

                input.StudentNumber = Optional(input.StudentNumber);
                input.Department = Optional(input.Department);
                input.EligibilityNotes = Optional(input.EligibilityNotes);
                if (
                    profile is null
                    && input.Role != RoleNames.Borrower
                    && (input.StudentNumber is not null || input.Department is not null || input.EligibilityNotes is not null)
                )
                {
                    throw new AdminUserEditException(Errors.BorrowerProfileRoleRequired);
                }

                if (
                    input.StudentNumber is not null
                    && await _accountRepository.StudentExistsAsync(input.StudentNumber, target.Id, cancellationToken)
                )
                {
                    errors.Add(new(nameof(input.StudentNumber), Errors.StudentNumberAlreadyRegistered));
                    return new(OperationOutcome.Invalid, Errors: errors);
                }

                var before = Snapshot(target, currentRoles, profile);
                var activeChanged = target.IsActive != input.IsActive;
                target.FirstName = input.FirstName.Trim();
                target.LastName = input.LastName.Trim();
                target.IsActive = input.IsActive;
                target.UpdatedAtUtc = DateTime.UtcNow;
                EnsureIdentityResult(await _userManager.UpdateAsync(target));
                if (roleChanged)
                {
                    if (currentRoles.Count > 0)
                    {
                        EnsureIdentityResult(await _userManager.RemoveFromRolesAsync(target, currentRoles));
                    }

                    EnsureIdentityResult(await _userManager.AddToRoleAsync(target, input.Role));
                }

                if (roleChanged || activeChanged)
                {
                    EnsureIdentityResult(await _userManager.UpdateSecurityStampAsync(target));
                }

                if (profile is null && input.Role == RoleNames.Borrower)
                {
                    profile = new BorrowerProfile { UserId = target.Id, ContactNumber = target.PhoneNumber };
                    _accountRepository.AddProfile(profile);
                    target.BorrowerProfile = profile;
                }

                if (profile is not null)
                {
                    profile.StudentNumber = input.StudentNumber;
                    profile.Department = input.Department;
                    profile.IsEligible = input.IsEligible;
                    profile.EligibilityNotes = input.EligibilityNotes;
                    profile.UpdatedAtUtc = DateTime.UtcNow;
                }

                _auditRepository.Add(
                    new AuditEvent
                    {
                        ActorUserId = actorId,
                        Action = "Admin.UserUpdated",
                        EntityType = "User",
                        EntityId = target.Id,
                        Outcome = "Succeeded",
                        BeforeJson = before,
                        AfterJson = Snapshot(target, [input.Role], profile),
                        IpAddress = actor.IpAddress,
                        CorrelationId = actor.CorrelationId,
                    }
                );
                await _unitOfWork.SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return OperationResult<string>.Ok(id, Common.AccountUpdated);
            }
            catch (AdminUserEditException exception)
            {
                _unitOfWork.ClearTracking();
                return OperationResult<string>.Invalid(exception.Message, reload: exception.IsStale);
            }
            catch (PersistenceException exception) when (exception.Failure is PersistenceFailure.StaleWrite or PersistenceFailure.Duplicate)
            {
                _unitOfWork.ClearTracking();
                return exception.Failure == PersistenceFailure.StaleWrite
                    ? OperationResult<string>.Invalid(Errors.StaleAccountEdit, reload: true)
                    : OperationResult<string>.Invalid(Errors.StudentNumberAlreadyRegistered, nameof(input.StudentNumber));
            }
        }

        public async Task<OperationResult<string>> CreateAsync(
            RequestContext actor,
            UserCreateModel input,
            CancellationToken cancellationToken
        )
        {
            var errors = new List<FieldError>();
            if (!PrimaryRoles.Contains(input.Role))
            {
                errors.Add(new(nameof(input.Role), Errors.InvalidRole));
            }

            input.StudentNumber = Optional(input.StudentNumber);
            input.Department = Optional(input.Department);
            input.ContactNumber = Optional(input.ContactNumber);
            if (input.Role != RoleNames.Borrower && (input.StudentNumber is not null || input.Department is not null))
            {
                errors.Add(new(nameof(input.StudentNumber), Errors.BorrowerFieldsOnly));
            }

            var email = input.Email.Trim();
            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                errors.Add(new(nameof(input.Email), Errors.EmailAlreadyRegistered));
            }

            if (
                input.StudentNumber is not null
                && await _accountRepository.StudentExistsAsync(input.StudentNumber, null, cancellationToken)
            )
            {
                errors.Add(new(nameof(input.StudentNumber), Errors.StudentNumberAlreadyRegistered));
            }

            if (errors.Count > 0)
            {
                return new(OperationOutcome.Invalid, Errors: errors);
            }

            await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
            var user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = input.FirstName.Trim(),
                LastName = input.LastName.Trim(),
                PhoneNumber = input.ContactNumber,
                ApprovalStatus = AccountApprovalStatus.Approved,
                IsActive = true,
            };
            var created = await _userManager.CreateAsync(user, input.Password);
            if (!created.Succeeded)
            {
                foreach (var error in created.Errors)
                {
                    errors.Add(new(error.Code.Contains("Password") ? nameof(input.Password) : string.Empty, error.Description));
                }

                return new(OperationOutcome.Invalid, Errors: errors);
            }

            if (!(await _userManager.AddToRoleAsync(user, input.Role)).Succeeded)
            {
                errors.Add(new(string.Empty, Errors.RoleAssignmentFailed));
                return new(OperationOutcome.Invalid, Errors: errors);
            }

            if (input.Role == RoleNames.Borrower)
            {
                _accountRepository.AddProfile(
                    new BorrowerProfile
                    {
                        UserId = user.Id,
                        StudentNumber = input.StudentNumber,
                        Department = input.Department,
                        ContactNumber = input.ContactNumber,
                        IsEligible = true,
                    }
                );
            }

            _auditRepository.Add(
                new AuditEvent
                {
                    ActorUserId = actor.UserId,
                    Action = "Admin.UserCreated",
                    EntityType = "User",
                    EntityId = user.Id,
                    Outcome = "Succeeded",
                    AfterJson = JsonSerializer.Serialize(
                        new
                        {
                            user.FirstName,
                            user.LastName,
                            user.Email,
                            input.Role,
                            input.StudentNumber,
                            input.Department,
                        }
                    ),
                    IpAddress = actor.IpAddress,
                    CorrelationId = actor.CorrelationId,
                }
            );
            await _unitOfWork.SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<string>.Ok(user.Id, $"{user.FullName} can now sign in as {input.Role}.");
        }

        public async Task<OperationResult<string>> DeleteAsync(RequestContext actor, string id, CancellationToken cancellationToken)
        {
            await using var transaction = await _unitOfWork.BeginAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                await AcquireLockAsync("CampusGear:UserAdministration", cancellationToken);
            }
            catch (AdminUserEditException exception)
            {
                return OperationResult<string>.Invalid(exception.Message);
            }

            var target = await _accountRepository.FindWithProfileAsync(id, cancellationToken);
            if (target is null)
            {
                return OperationResult<string>.Missing();
            }

            if (target.Id == actor.UserId)
            {
                return OperationResult<string>.Invalid(Errors.SelfDeletionForbidden);
            }

            var roles = await _userManager.GetRolesAsync(target);
            if (
                roles.Contains(RoleNames.Administrator)
                && target.IsActive
                && !await _accountRepository.HasOtherAdministratorAsync(target.Id, cancellationToken)
            )
            {
                return OperationResult<string>.Invalid(Errors.LastAdministratorRequired);
            }

            var hasHistory = await _accountRepository.HasHistoryAsync(target.Id, cancellationToken);
            var before = Snapshot(target, roles, target.BorrowerProfile);
            if (hasHistory)
            {
                target.IsActive = false;
                target.UpdatedAtUtc = DateTime.UtcNow;
                await _userManager.UpdateAsync(target);
                await _userManager.UpdateSecurityStampAsync(target);
            }
            else
            {
                await _accountRepository.PrepareDeletionAsync(target, cancellationToken);
                await _unitOfWork.SaveAsync(cancellationToken);
                var deleted = await _userManager.DeleteAsync(target);
                if (!deleted.Succeeded)
                {
                    return OperationResult<string>.Invalid(Errors.DeletionFailed);
                }
            }

            _auditRepository.Add(
                new AuditEvent
                {
                    ActorUserId = actor.UserId,
                    Action = hasHistory ? "Admin.UserDeactivated" : "Admin.UserDeleted",
                    EntityType = "User",
                    EntityId = id,
                    Outcome = "Succeeded",
                    BeforeJson = before,
                    IpAddress = actor.IpAddress,
                    CorrelationId = actor.CorrelationId,
                }
            );
            await _unitOfWork.SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<string>.Ok(
                id,
                hasHistory
                    ? $"{target.FullName} has borrowing history, so the account was deactivated instead of deleted. They can no longer sign in."
                    : $"{target.FullName}'s account was deleted."
            );
        }

        public async Task<UserEditPage?> GetEditAsync(
            RequestContext actor,
            string id,
            UserEditModel? input,
            CancellationToken cancellationToken
        )
        {
            var target = await _accountRepository.FindWithProfileFreshAsync(id, cancellationToken);
            if (target is null)
            {
                return null;
            }

            var currentRoles = await _userManager.GetRolesAsync(target);
            var currentRole = PrimaryRoles.LastOrDefault(currentRoles.Contains) ?? RoleNames.Borrower;
            input ??= new UserEditModel
            {
                FirstName = target.FirstName,
                LastName = target.LastName,
                Role = currentRole,
                IsActive = target.IsActive,
                StudentNumber = target.BorrowerProfile?.StudentNumber,
                Department = target.BorrowerProfile?.Department,
                IsEligible = target.BorrowerProfile?.IsEligible ?? true,
                EligibilityNotes = target.BorrowerProfile?.EligibilityNotes,
                ConcurrencyStamp = target.ConcurrencyStamp ?? string.Empty,
                ProfileRowVersion = target.BorrowerProfile is null
                    ? string.Empty
                    : Convert.ToBase64String(target.BorrowerProfile.RowVersion),
            };
            return new UserEditPage(
                id,
                target.Email ?? string.Empty,
                target.ApprovalStatus,
                target.PhoneNumber,
                string.Join(", ", currentRoles),
                target.BorrowerProfile is not null,
                target.Id == actor.UserId,
                input
            );
        }

        private async Task AcquireLockAsync(string resourceName, CancellationToken ct)
        {
            try
            {
                await _lockManager.AcquireAsync(resourceName, ct);
            }
            catch (PersistenceException e) when (e.Failure == PersistenceFailure.Busy)
            {
                throw new AdminUserEditException(Errors.AdministrationBusy);
            }
        }

        private static void EnsureIdentityResult(IdentityResult result)
        {
            if (result.Succeeded)
            {
                return;
            }

            if (result.Errors.Any(x => x.Code == "ConcurrencyFailure"))
            {
                throw new AdminUserEditException(Errors.StaleAccountEdit, true);
            }

            throw new AdminUserEditException(Errors.UpdateFailed);
        }

        private static bool SameRowVersion(byte[]? current, string? submitted)
        {
            if (current is null)
            {
                return string.IsNullOrEmpty(submitted);
            }

            try
            {
                return current.AsSpan().SequenceEqual(Convert.FromBase64String(submitted ?? string.Empty));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string Snapshot(User user, IEnumerable<string> roles, BorrowerProfile? profile)
        {
            return JsonSerializer.Serialize(
                new
                {
                    user.FirstName,
                    user.LastName,
                    user.IsActive,
                    user.ApprovalStatus,
                    user.EmailConfirmed,
                    Roles = roles.Order().ToArray(),
                    Borrower = profile is null
                        ? null
                        : new
                        {
                            profile.StudentNumber,
                            profile.Department,
                            profile.IsEligible,
                            profile.EligibilityNotes,
                        },
                }
            );
        }

        private static string SearchText(string? value)
        {
            var text = value?.Trim() ?? string.Empty;
            return text.Length > 120 ? text[..120] : text;
        }

        private static string? Optional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private sealed class AdminUserEditException : Exception
        {
            public AdminUserEditException(string message, bool isStale = false)
                : base(message)
            {
                IsStale = isStale;
            }

            public bool IsStale { get; }
        }
    }
}
