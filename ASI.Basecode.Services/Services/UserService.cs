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

// =====================================================================
// USER SERVICE  -  everything the admin does to accounts (Users & Roles page).
// List, approve/reject sign-ups, create, edit (name, role, active, borrowing access), delete.
//
// Two switches on a student account:
//   Active off ("deactivate")  -> can't sign in at all.
//   Can reserve off ("pause")  -> can sign in, but can't book.
// Turning either off CANCELS the student's Pending and Approved bookings (CancelOpenBookings).
// Deactivating is refused while the student still has an item on loan.
// =====================================================================
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

        // The account list (search, role filter, status filter, paging).
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

        // Approve or reject a new student registration. Approved = can sign in.
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

        // EDIT an account. Checks, in order:
        //   - the admin is still a valid administrator
        //   - nobody else saved this account meanwhile (concurrency stamp / row version)
        //   - you can't lock yourself out, and at least one admin must remain
        //   - deactivating is refused while the student has an item on loan
        // Then saves, and cancels open bookings if the student was deactivated or paused.
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
                // One admin edit at a time, plus the student's own lock (the same lock new bookings take).
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

                // Is this save switching the account off, or pausing borrowing?
                var deactivating = target.IsActive && !input.IsActive;
                var pausing = profile is { IsEligible: true } && !input.IsEligible;

                // Equipment on loan is physically with the student, so it cannot be cancelled away. Pausing is still
                // allowed: the student keeps signing in and sees the due date.
                if (deactivating && profile is not null && await _accountRepository.HasOpenLoanAsync(target.Id, cancellationToken))
                {
                    throw new AdminUserEditException(Errors.OpenLoanPreventsAccountDeactivation);
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

                var cancelled =
                    (deactivating || pausing) && profile is not null
                        ? CancelOpenBookings(
                            actor,
                            await _accountRepository.OpenBookingsAsync(target.Id, cancellationToken),
                            deactivating ? "Cancelled automatically: the account was deactivated." : "Cancelled automatically: borrowing access was paused."
                        )
                        : 0;

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
                return OperationResult<string>.Ok(id, Common.AccountUpdated + CancelledSuffix(cancelled));
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

        // CREATE an account from the admin page. It is approved and verified immediately (no email step).
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

        // DELETE: an account with any history (bookings, loans, repairs) is only deactivated, so the
        // records stay. An account with no history is removed for good.
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

            // Any booking is history, so a borrower with bookings is deactivated here rather than deleted. Take the lock
            // new bookings take, so none can slip in between this check and the cancellation below.
            if (target.IsActive && target.BorrowerProfile is { } profile)
            {
                try
                {
                    await AcquireLockAsync($"CampusGear:BorrowerProfile:{profile.Id:N}", cancellationToken);
                }
                catch (AdminUserEditException exception)
                {
                    return OperationResult<string>.Invalid(exception.Message);
                }

                if (await _accountRepository.HasOpenLoanAsync(target.Id, cancellationToken))
                {
                    return OperationResult<string>.Invalid(Errors.OpenLoanPreventsAccountDeactivation);
                }
            }

            var hasHistory = await _accountRepository.HasHistoryAsync(target.Id, cancellationToken);
            var before = Snapshot(target, roles, target.BorrowerProfile);
            var cancelled = 0;
            if (hasHistory)
            {
                if (target.IsActive && target.BorrowerProfile is not null)
                {
                    cancelled = CancelOpenBookings(
                        actor,
                        await _accountRepository.OpenBookingsAsync(target.Id, cancellationToken),
                        "Cancelled automatically: the account was deactivated."
                    );
                }

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
                        + CancelledSuffix(cancelled)
                    : $"{target.FullName}'s account was deleted."
            );
        }

        /// <summary>
        /// Cancels pending and approved bookings as the administrator, with the same fields and audit event a
        /// borrower's own cancellation records. The caller holds the borrower-profile lock, which approval and
        /// release also take before re-checking eligibility, so neither can act on these bookings concurrently.
        /// </summary>
        private int CancelOpenBookings(RequestContext actor, IReadOnlyList<Reservation> bookings, string reason)
        {
            var now = DateTime.UtcNow;
            foreach (var reservation in bookings)
            {
                var before = reservation.Status;
                reservation.Status = ReservationStatus.Cancelled;
                reservation.HoldExpiresAtUtc = null;
                reservation.CancelledAtUtc = now;
                reservation.DecisionReason = reason;
                reservation.UpdatedAtUtc = now;
                _auditRepository.Add(
                    new AuditEvent
                    {
                        ActorUserId = actor.UserId,
                        Action = "reservation.cancelled",
                        EntityType = nameof(Reservation),
                        EntityId = reservation.Id.ToString("D"),
                        Outcome = "Succeeded",
                        BeforeJson = JsonSerializer.Serialize(new { Status = before.ToString() }),
                        AfterJson = JsonSerializer.Serialize(
                            new
                            {
                                Status = reservation.Status.ToString(),
                                reservation.EquipmentItemId,
                                reservation.BorrowerProfileId,
                                reservation.StartAtUtc,
                                reservation.EndAtUtc,
                                reservation.HoldExpiresAtUtc,
                            }
                        ),
                        IpAddress = actor.IpAddress,
                        CorrelationId = actor.CorrelationId,
                    }
                );
            }

            return bookings.Count;
        }

        private static string CancelledSuffix(int count) =>
            count switch
            {
                0 => "",
                1 => " Their 1 pending or approved booking was cancelled.",
                _ => $" Their {count} pending or approved bookings were cancelled.",
            };

        // Loads the account for the Edit modal, plus how many bookings saving would cancel (for the warning).
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
            var openBookings = target.BorrowerProfile is null
                ? 0
                : await _accountRepository.CountOpenBookingsAsync(target.Id, cancellationToken);
            return new UserEditPage(
                id,
                target.Email ?? string.Empty,
                target.ApprovalStatus,
                target.PhoneNumber,
                string.Join(", ", currentRoles),
                target.BorrowerProfile is not null,
                target.Id == actor.UserId,
                input,
                ManilaTime.Format(target.CreatedAtUtc),
                target.IsActive ? openBookings : 0,
                target.BorrowerProfile?.IsEligible == true ? openBookings : 0
            );
        }

        // Takes a named lock; if it can't get it within 10 seconds, shows "busy, try again".
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

        // A JSON picture of the account for the audit log (before/after).
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
