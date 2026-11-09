using System.Data;
using System.Globalization;
using System.Text.Json;
using CampusGear.Controllers;
using CampusGear.Data.Interfaces;
using CampusGear.Models;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;
namespace CampusGear.Services;
public sealed class UserAdministrationService(IAccountRepository accounts, IAuditRepository audits, IUnitOfWork work, IWorkflowLockManager locks, UserManager<ApplicationUser> users) : IUserAdministrationService
{
    private static readonly string[] PrimaryRoles = ["Borrower", "Custodian", "Administrator"];
    private static readonly TimeZoneInfo ManilaZone = ResolveManilaZone();
    public async Task<AdminUsersPage> GetPageAsync(string? q, string? role, string? status,
        int page = 1, CancellationToken cancellationToken = default)
    {
        q = SearchText(q);
        role = PrimaryRoles.Contains(role) ? role! : "all";
        status = status is "active" or "inactive" or "pending" or "rejected" ? status : "all";
        var data = await accounts.ListAsync(q, role, status, page, cancellationToken);
        var accountRows = data.Accounts; var roleRows = data.Roles;
        var total = data.Total; var pendingTotal = data.Pending; var pages = data.Pages; page = data.Page;
        var rows = accountRows.Select(x => new AdminUserRow(
            x.Id, x.FullName, x.Email ?? string.Empty,
            string.Join(", ", roleRows.Where(r => r.UserId == x.Id).Select(r => r.Name).Order()),
            x.IsActive, x.ApprovalStatus, x.BorrowerProfile?.StudentNumber,
            x.BorrowerProfile?.Department, x.BorrowerProfile?.IsEligible, CustodianTime.Format(x.CreatedAtUtc))).ToList();
        return new AdminUsersPage(rows, q, role, status, page, pages, total, pendingTotal,
            null, null);
    }

    public async Task<OperationResult<string>> DecideAsync(RequestContext actor, string id, AccountApprovalStatus decision, CancellationToken cancellationToken)
    {
        var target = await accounts.FindAsync(id, cancellationToken);
        if (target is null) return OperationResult<string>.Missing();

        // Pending registrations can be approved or rejected; a rejected registration can still be approved later.
        var allowed = target.ApprovalStatus == AccountApprovalStatus.PendingApproval ||
            decision == AccountApprovalStatus.Approved && target.ApprovalStatus == AccountApprovalStatus.Rejected;
        if (!allowed)
        {
            return OperationResult<string>.Invalid($"{target.FullName}'s registration has already been decided.");
        }

        var before = JsonSerializer.Serialize(new { target.ApprovalStatus, target.EmailConfirmed });
        target.ApprovalStatus = decision;
        // Administrator approval replaces sign-up email verification.
        if (decision == AccountApprovalStatus.Approved) target.EmailConfirmed = true;
        target.UpdatedAtUtc = DateTime.UtcNow;
        var updated = await users.UpdateAsync(target);
        if (!updated.Succeeded)
        {
            return OperationResult<string>.Invalid("This account changed while the page was open. Review it and try again.");
        }

        var approved = decision == AccountApprovalStatus.Approved;
        audits.Add(new AuditEvent
        {
            ActorUserId = actor.UserId,
            Action = approved ? "Admin.RegistrationApproved" : "Admin.RegistrationRejected",
            EntityType = "ApplicationUser",
            EntityId = target.Id,
            Outcome = "Succeeded",
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(new { target.ApprovalStatus, target.EmailConfirmed }),
            IpAddress = actor.IpAddress,
            CorrelationId = actor.CorrelationId
        });
        await work.SaveAsync(cancellationToken);
        return OperationResult<string>.Ok(id, approved
            ? $"{target.FullName}'s account was approved."
            : $"{target.FullName}'s registration was rejected.");
    }

    public async Task<OperationResult<string>> EditAsync(RequestContext actor, string id, UserEditCommand input,
        CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        if (!PrimaryRoles.Contains(input.Role))
            errors.Add(new(nameof(input.Role), "Choose Borrower, Custodian, or Administrator."));
        if (errors.Count > 0) return new(OperationOutcome.Invalid, Errors: errors);

        try
        {
            await using var transaction = await work.BeginAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
            await AcquireLockAsync("CampusGear:UserAdministration", cancellationToken);

            var actorId = actor.UserId;
            var actorAccount = actorId is null ? null : await accounts.FindFreshAsync(actorId, cancellationToken);
            if (actorAccount is null || !actorAccount.IsActive || !actorAccount.EmailConfirmed ||
                !await users.IsInRoleAsync(actorAccount, "Administrator"))
                return new(OperationOutcome.Forbidden);

            var target = await accounts.FindWithProfileAsync(id, cancellationToken);
            if (target is null) return OperationResult<string>.Missing();
            var profile = target.BorrowerProfile;
            if (profile is not null)
            {
                await AcquireLockAsync($"CampusGear:BorrowerProfile:{profile.Id:N}", cancellationToken);
                await accounts.ReloadProfileAsync(profile, cancellationToken);
            }

            if (!string.Equals(input.ConcurrencyStamp, target.ConcurrencyStamp, StringComparison.Ordinal) ||
                !SameRowVersion(profile?.RowVersion, input.ProfileRowVersion))
                throw new AdminUserEditException("This account changed while the form was open. Review the latest details and try again.", true);

            var currentRoles = await users.GetRolesAsync(target);
            var roleChanged = currentRoles.Count != 1 || currentRoles[0] != input.Role;
            if (target.Id == actorId && (!input.IsActive || input.Role != "Administrator"))
                throw new AdminUserEditException("You cannot deactivate your own account or remove your own Administrator role.");
            if (input.Role != "Borrower" && target.ApprovalStatus != AccountApprovalStatus.Approved)
                throw new AdminUserEditException("The account must be approved before receiving a staff role.");

            if (target.IsActive && currentRoles.Contains("Administrator") &&
                (!input.IsActive || input.Role != "Administrator"))
            {
                var otherAdministrator = await accounts.HasOtherAdministratorAsync(target.Id, cancellationToken);
                if (!otherAdministrator)
                    throw new AdminUserEditException("Keep at least one active, verified Administrator account.");
            }

            input.StudentNumber = Optional(input.StudentNumber);
            input.Department = Optional(input.Department);
            input.EligibilityNotes = Optional(input.EligibilityNotes);
            if (profile is null && input.Role != "Borrower" &&
                (input.StudentNumber is not null || input.Department is not null || input.EligibilityNotes is not null))
                throw new AdminUserEditException("Borrower profile details apply when Borrower is selected. Existing profiles are retained when roles change.");
            if (input.StudentNumber is not null && await accounts.StudentExistsAsync(input.StudentNumber, target.Id, cancellationToken))
            {
                errors.Add(new(nameof(input.StudentNumber), "This Student ID Number is already registered."));
                return new(OperationOutcome.Invalid, Errors: errors);
            }

            var before = Snapshot(target, currentRoles, profile);
            var activeChanged = target.IsActive != input.IsActive;
            target.FirstName = input.FirstName.Trim();
            target.LastName = input.LastName.Trim();
            target.IsActive = input.IsActive;
            target.UpdatedAtUtc = DateTime.UtcNow;
            EnsureIdentityResult(await users.UpdateAsync(target));

            if (roleChanged)
            {
                if (currentRoles.Count > 0)
                    EnsureIdentityResult(await users.RemoveFromRolesAsync(target, currentRoles));
                EnsureIdentityResult(await users.AddToRoleAsync(target, input.Role));
            }
            if (roleChanged || activeChanged)
                EnsureIdentityResult(await users.UpdateSecurityStampAsync(target));

            if (profile is null && input.Role == "Borrower")
            {
                profile = new BorrowerProfile { UserId = target.Id, ContactNumber = target.PhoneNumber };
                accounts.AddProfile(profile);
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

            audits.Add(new AuditEvent
            {
                ActorUserId = actorId,
                Action = "Admin.UserUpdated",
                EntityType = "ApplicationUser",
                EntityId = target.Id,
                Outcome = "Succeeded",
                BeforeJson = before,
                AfterJson = Snapshot(target, [input.Role], profile),
                IpAddress = actor.IpAddress,
                CorrelationId = actor.CorrelationId
            });
            await work.SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<string>.Ok(id, "Account details and access permissions updated.");
        }
        catch (AdminUserEditException exception)
        {
            work.ClearTracking();
            return OperationResult<string>.Invalid(exception.Message, reload: exception.IsStale);
        }
        catch (PersistenceException exception) when (exception.Failure is PersistenceFailure.StaleWrite or PersistenceFailure.Duplicate)
        {
            work.ClearTracking();
            return exception.Failure == PersistenceFailure.StaleWrite
                ? OperationResult<string>.Invalid("This account changed while the form was open. Review the latest details and try again.", reload: true)
                : OperationResult<string>.Invalid("This Student ID Number is already registered.", nameof(input.StudentNumber));
        }
    }

    public async Task<OperationResult<string>> CreateAsync(RequestContext actor, UserCreateCommand input, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        if (!PrimaryRoles.Contains(input.Role))
            errors.Add(new(nameof(input.Role), "Choose Borrower, Custodian, or Administrator."));
        input.StudentNumber = Optional(input.StudentNumber);
        input.Department = Optional(input.Department);
        input.ContactNumber = Optional(input.ContactNumber);
        if (input.Role != "Borrower" && (input.StudentNumber is not null || input.Department is not null))
            errors.Add(new(nameof(input.StudentNumber), "Student ID Number and department apply to borrowers only."));
        var email = input.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
            errors.Add(new(nameof(input.Email), "An account already uses this email address."));
        if (input.StudentNumber is not null &&
            await accounts.StudentExistsAsync(input.StudentNumber, null, cancellationToken))
            errors.Add(new(nameof(input.StudentNumber), "This Student ID Number is already registered."));
        if (errors.Count > 0) return new(OperationOutcome.Invalid, Errors: errors);

        await using var transaction = await work.BeginAsync(cancellationToken);
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            PhoneNumber = input.ContactNumber,
            ApprovalStatus = AccountApprovalStatus.Approved,
            IsActive = true
        };
        var created = await users.CreateAsync(user, input.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
                errors.Add(new(error.Code.Contains("Password") ? nameof(input.Password) : string.Empty, error.Description));
            return new(OperationOutcome.Invalid, Errors: errors);
        }
        if (!(await users.AddToRoleAsync(user, input.Role)).Succeeded)
        {
            errors.Add(new(string.Empty, "The role could not be assigned. Please try again."));
            return new(OperationOutcome.Invalid, Errors: errors);
        }
        if (input.Role == "Borrower")
            accounts.AddProfile(new BorrowerProfile
            {
                UserId = user.Id,
                StudentNumber = input.StudentNumber,
                Department = input.Department,
                ContactNumber = input.ContactNumber,
                IsEligible = true
            });
        audits.Add(new AuditEvent
        {
            ActorUserId = actor.UserId,
            Action = "Admin.UserCreated",
            EntityType = "ApplicationUser",
            EntityId = user.Id,
            Outcome = "Succeeded",
            AfterJson = JsonSerializer.Serialize(new { user.FirstName, user.LastName, user.Email, input.Role, input.StudentNumber, input.Department }),
            IpAddress = actor.IpAddress,
            CorrelationId = actor.CorrelationId
        });
        await work.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<string>.Ok(user.Id, $"{user.FullName} can now sign in as {input.Role}.");
    }

    public async Task<OperationResult<string>> DeleteAsync(RequestContext actor, string id, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try { await AcquireLockAsync("CampusGear:UserAdministration", cancellationToken); }
        catch (AdminUserEditException exception) { return OperationResult<string>.Invalid(exception.Message); }

        var target = await accounts.FindWithProfileAsync(id, cancellationToken);
        if (target is null) return OperationResult<string>.Missing();
        if (target.Id == actor.UserId)
        {
            return OperationResult<string>.Invalid("You cannot delete your own account.");
        }
        var roles = await users.GetRolesAsync(target);
        if (roles.Contains("Administrator") && target.IsActive && !await accounts.HasOtherAdministratorAsync(target.Id, cancellationToken))
        {
            return OperationResult<string>.Invalid("Keep at least one active, verified Administrator account.");
        }

        var hasHistory = await accounts.HasHistoryAsync(target.Id, cancellationToken);
        var before = Snapshot(target, roles, target.BorrowerProfile);
        if (hasHistory)
        {
            target.IsActive = false;
            target.UpdatedAtUtc = DateTime.UtcNow;
            await users.UpdateAsync(target);
            await users.UpdateSecurityStampAsync(target);
        }
        else
        {
            await accounts.PrepareDeletionAsync(target, cancellationToken);
            await work.SaveAsync(cancellationToken);
            var deleted = await users.DeleteAsync(target);
            if (!deleted.Succeeded)
            {
                return OperationResult<string>.Invalid("The account could not be deleted. Please try again.");
            }
        }
        audits.Add(new AuditEvent
        {
            ActorUserId = actor.UserId,
            Action = hasHistory ? "Admin.UserDeactivated" : "Admin.UserDeleted",
            EntityType = "ApplicationUser",
            EntityId = id,
            Outcome = "Succeeded",
            BeforeJson = before,
            IpAddress = actor.IpAddress,
            CorrelationId = actor.CorrelationId
        });
        await work.SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<string>.Ok(id, hasHistory
            ? $"{target.FullName} has borrowing history, so the account was deactivated instead of deleted. They can no longer sign in."
            : $"{target.FullName}'s account was deleted.");
    }

    public async Task<AdminAuditPage> GetAuditAsync(string? q, int page = 1,
        CancellationToken cancellationToken = default)
    {
        q = SearchText(q);
        var data = await audits.ListAsync(q, page, cancellationToken);
        var events = data.Events; var total = data.Total; var pages = data.Pages; page = data.Page;
        var rows = events.Select(x => new AdminAuditRow(x.Id, Manila(x.CreatedAtUtc), x.Action,
            x.ActorUser?.FullName ?? "Unauthenticated / system", x.ActorUser?.Email,
            x.EntityType, x.EntityId, x.Outcome, x.IpAddress, x.CorrelationId,
            PrettyJson(x.BeforeJson), PrettyJson(x.AfterJson))).ToList();
        return new AdminAuditPage(rows, q, page, pages, total);
    }

    public async Task<UserEditPage?> GetEditAsync(RequestContext actor, string id, UserEditCommand? input,
        CancellationToken cancellationToken)
    {
        var target = await accounts.FindWithProfileFreshAsync(id, cancellationToken);
        if (target is null) return null;
        var currentRoles = await users.GetRolesAsync(target);
        var currentRole = PrimaryRoles.LastOrDefault(currentRoles.Contains) ?? "Borrower";
        input ??= new UserEditCommand
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
            ProfileRowVersion = target.BorrowerProfile is null ? string.Empty : Convert.ToBase64String(target.BorrowerProfile.RowVersion)
        };
        return new UserEditPage(id, target.Email ?? string.Empty,
            target.ApprovalStatus, target.PhoneNumber, string.Join(", ", currentRoles),
            target.BorrowerProfile is not null, target.Id == actor.UserId, input);
    }

    private async Task AcquireLockAsync(string resourceName, CancellationToken ct)
    {
        try { await locks.AcquireAsync(resourceName, ct); }
        catch (PersistenceException e) when (e.Failure == PersistenceFailure.Busy)
        { throw new AdminUserEditException("Account administration is busy. Please try again."); }
    }


    private static void EnsureIdentityResult(IdentityResult result)
    {
        if (result.Succeeded) return;
        if (result.Errors.Any(x => x.Code == "ConcurrencyFailure"))
            throw new AdminUserEditException("This account changed while the form was open. Review the latest details and try again.", true);
        throw new AdminUserEditException("We could not update the account. Please try again.");
    }

    private static bool SameRowVersion(byte[]? current, string? submitted)
    {
        if (current is null) return string.IsNullOrEmpty(submitted);
        try { return current.AsSpan().SequenceEqual(Convert.FromBase64String(submitted ?? string.Empty)); }
        catch (FormatException) { return false; }
    }

    private static string Snapshot(ApplicationUser user, IEnumerable<string> roles, BorrowerProfile? profile) =>
        JsonSerializer.Serialize(new
        {
            user.FirstName,
            user.LastName,
            user.IsActive,
            user.ApprovalStatus,
            user.EmailConfirmed,
            Roles = roles.Order().ToArray(),
            Borrower = profile is null ? null : new
            {
                profile.StudentNumber,
                profile.Department,
                profile.IsEligible,
                profile.EligibilityNotes
            }
        });

    private static string SearchText(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length > 120 ? text[..120] : text;
    }
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? PrettyJson(string? value)
    {
        if (value is null) return null;
        try { using var document = JsonDocument.Parse(value); return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true }); }
        catch (JsonException) { return value; }
    }
    private static string Manila(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(utc, DateTimeKind.Utc), ManilaZone).ToString("MMM d, yyyy · h:mm:ss tt", CultureInfo.InvariantCulture);
    private static TimeZoneInfo ResolveManilaZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
    }
    private sealed class AdminUserEditException(string message, bool isStale = false) : Exception(message)
    {
        public bool IsStale { get; } = isStale;
    }
}
