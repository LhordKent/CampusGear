using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Controllers;

[Authorize(Roles = "Administrator")]
[AutoValidateAntiforgeryToken]
public sealed class AdminUsersController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users) : Controller
{
    private static readonly string[] PrimaryRoles = ["Borrower", "Custodian", "Administrator"];
    private static readonly TimeZoneInfo ManilaZone = ResolveManilaZone();
    private const int UserPageSize = 15;
    private const int AuditPageSize = 25;

    [HttpGet("admin/borrowers")]
    public Task<IActionResult> Borrowers(string? q, string? status, int page = 1,
        CancellationToken cancellationToken = default) => Index(q, "Borrower", status, page, cancellationToken);

    [HttpGet("admin/users")]
    public async Task<IActionResult> Index(string? q, string? role, string? status,
        int page = 1, CancellationToken cancellationToken = default)
    {
        q = SearchText(q);
        role = PrimaryRoles.Contains(role) ? role! : "all";
        status = status is "active" or "inactive" or "pending" or "rejected" ? status : "all";
        var query = db.Users.AsNoTracking();
        if (q.Length > 0)
            query = query.Where(x => x.FullName.Contains(q) || x.Email!.Contains(q) ||
                x.BorrowerProfile != null &&
                (x.BorrowerProfile.StudentNumber != null && x.BorrowerProfile.StudentNumber.Contains(q) ||
                 x.BorrowerProfile.Department != null && x.BorrowerProfile.Department.Contains(q)));
        if (role != "all")
            query = query.Where(x => db.UserRoles.Any(ur => ur.UserId == x.Id &&
                db.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));
        query = status switch
        {
            "active" => query.Where(x => x.IsActive && x.ApprovalStatus == AccountApprovalStatus.Approved),
            "inactive" => query.Where(x => !x.IsActive),
            "pending" => query.Where(x => x.ApprovalStatus == AccountApprovalStatus.PendingApproval),
            "rejected" => query.Where(x => x.ApprovalStatus == AccountApprovalStatus.Rejected),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        var pendingTotal = await db.Users.CountAsync(
            x => x.ApprovalStatus == AccountApprovalStatus.PendingApproval, cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)UserPageSize));
        page = Math.Clamp(page, 1, pages);
        var accounts = await query.Include(x => x.BorrowerProfile)
            // Newest registrations first, so the latest submissions are reviewed at the top.
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.FullName).ThenBy(x => x.Id)
            .Skip((page - 1) * UserPageSize).Take(UserPageSize).ToListAsync(cancellationToken);
        var ids = accounts.Select(x => x.Id).ToArray();
        var roleRows = await db.UserRoles.AsNoTracking().Where(x => ids.Contains(x.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);
        var rows = accounts.Select(x => new AdminUserRow(
            x.Id, x.FullName, x.Email ?? string.Empty,
            string.Join(", ", roleRows.Where(r => r.UserId == x.Id).Select(r => r.Name).Order()),
            x.IsActive, x.ApprovalStatus, x.BorrowerProfile?.StudentNumber,
            x.BorrowerProfile?.Department, x.BorrowerProfile?.IsEligible, CustodianTime.Format(x.CreatedAtUtc))).ToList();
        return View("Index", new AdminUsersPage(rows, q, role, status, page, pages, total, pendingTotal,
            TempData["AdminUsersFeedback"] as string, TempData["AdminUsersError"] as string));
    }

    [HttpPost("admin/users/{id}/approve")]
    public Task<IActionResult> Approve(string id, string? returnUrl, CancellationToken cancellationToken) =>
        DecideRegistrationAsync(id, AccountApprovalStatus.Approved, returnUrl, cancellationToken);

    [HttpPost("admin/users/{id}/reject")]
    public Task<IActionResult> Reject(string id, string? returnUrl, CancellationToken cancellationToken) =>
        DecideRegistrationAsync(id, AccountApprovalStatus.Rejected, returnUrl, cancellationToken);

    private async Task<IActionResult> DecideRegistrationAsync(string id, AccountApprovalStatus decision,
        string? returnUrl, CancellationToken cancellationToken)
    {
        var back = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin/users?status=pending";
        var target = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (target is null) return NotFound();

        // Pending registrations can be approved or rejected; a rejected registration can still be approved later.
        var allowed = target.ApprovalStatus == AccountApprovalStatus.PendingApproval ||
            decision == AccountApprovalStatus.Approved && target.ApprovalStatus == AccountApprovalStatus.Rejected;
        if (!allowed)
        {
            TempData["AdminUsersError"] = $"{target.FullName}'s registration has already been decided.";
            return Redirect(back);
        }

        var before = JsonSerializer.Serialize(new { target.ApprovalStatus, target.EmailConfirmed });
        target.ApprovalStatus = decision;
        // Administrator approval replaces sign-up email verification.
        if (decision == AccountApprovalStatus.Approved) target.EmailConfirmed = true;
        target.UpdatedAtUtc = DateTime.UtcNow;
        var updated = await users.UpdateAsync(target);
        if (!updated.Succeeded)
        {
            TempData["AdminUsersError"] = "This account changed while the page was open. Review it and try again.";
            return Redirect(back);
        }

        var approved = decision == AccountApprovalStatus.Approved;
        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = users.GetUserId(User),
            Action = approved ? "Admin.RegistrationApproved" : "Admin.RegistrationRejected",
            EntityType = "ApplicationUser",
            EntityId = target.Id,
            Outcome = "Succeeded",
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(new { target.ApprovalStatus, target.EmailConfirmed }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = HttpContext.TraceIdentifier
        });
        await db.SaveChangesAsync(cancellationToken);
        TempData["AdminUsersFeedback"] = approved
            ? $"{target.FullName}'s account was approved."
            : $"{target.FullName}'s registration was rejected.";
        return Redirect(back);
    }

    [HttpGet("admin/users/{id}/edit")]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken) =>
        await EditViewAsync(id, null, cancellationToken);

    [HttpPost("admin/users/{id}/edit")]
    public async Task<IActionResult> Edit(string id, AdminUserEditInput input,
        CancellationToken cancellationToken)
    {
        if (!PrimaryRoles.Contains(input.Role))
            ModelState.AddModelError(nameof(input.Role), "Choose Borrower, Custodian, or Administrator.");
        if (!ModelState.IsValid) return await EditViewAsync(id, input, cancellationToken);

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
            await AcquireLockAsync("CampusGear:UserAdministration", cancellationToken);

            var actorId = users.GetUserId(User);
            var actor = actorId is null ? null : await db.Users.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == actorId, cancellationToken);
            if (actor is null || !actor.IsActive || !actor.EmailConfirmed ||
                !await users.IsInRoleAsync(actor, "Administrator"))
                return Forbid();

            var target = await db.Users.Include(x => x.BorrowerProfile)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (target is null) return NotFound();
            var profile = target.BorrowerProfile;
            if (profile is not null)
            {
                await AcquireLockAsync($"CampusGear:BorrowerProfile:{profile.Id:N}", cancellationToken);
                await db.Entry(profile).ReloadAsync(cancellationToken);
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
                var otherAdministrator = await db.UserRoles.AnyAsync(ur => ur.UserId != target.Id &&
                    db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Administrator") &&
                    db.Users.Any(u => u.Id == ur.UserId && u.IsActive && u.EmailConfirmed), cancellationToken);
                if (!otherAdministrator)
                    throw new AdminUserEditException("Keep at least one active, verified Administrator account.");
            }

            input.StudentNumber = Optional(input.StudentNumber);
            input.Department = Optional(input.Department);
            input.EligibilityNotes = Optional(input.EligibilityNotes);
            if (profile is null && input.Role != "Borrower" &&
                (input.StudentNumber is not null || input.Department is not null || input.EligibilityNotes is not null))
                throw new AdminUserEditException("Borrower profile details apply when Borrower is selected. Existing profiles are retained when roles change.");
            if (input.StudentNumber is not null && await db.BorrowerProfiles.AnyAsync(
                    x => x.UserId != target.Id && x.StudentNumber == input.StudentNumber, cancellationToken))
            {
                ModelState.AddModelError(nameof(input.StudentNumber), "This Student ID Number is already registered.");
                return await EditViewAsync(id, input, cancellationToken);
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
                db.BorrowerProfiles.Add(profile);
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

            db.AuditEvents.Add(new AuditEvent
            {
                ActorUserId = actorId,
                Action = "Admin.UserUpdated",
                EntityType = "ApplicationUser",
                EntityId = target.Id,
                Outcome = "Succeeded",
                BeforeJson = before,
                AfterJson = Snapshot(target, [input.Role], profile),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                CorrelationId = HttpContext.TraceIdentifier
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            TempData["AdminUsersFeedback"] = "Account details and access permissions updated.";
            return Redirect("/admin/users");
        }
        catch (AdminUserEditException exception)
        {
            db.ChangeTracker.Clear();
            if (exception.IsStale)
            {
                ModelState.Clear();
            }
            ModelState.AddModelError(string.Empty, exception.Message);
            return await EditViewAsync(id, exception.IsStale ? null : input, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            ModelState.Clear();
            ModelState.AddModelError(string.Empty, "This account changed while the form was open. Review the latest details and try again.");
            return await EditViewAsync(id, null, cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError(nameof(input.StudentNumber), "This Student ID Number is already registered.");
            return await EditViewAsync(id, input, cancellationToken);
        }
    }

    /// <summary>User and Role Management · Administrator only: create an account with its role. Created accounts
    /// are approved and verified at once; borrowers also get their profile.</summary>
    [HttpGet("admin/users/new")]
    public IActionResult Create(string? role) => View("Create", new AdminUserCreateInput
    {
        Role = PrimaryRoles.Contains(role) ? role! : "Borrower"
    });

    [HttpPost("admin/users/new")]
    public async Task<IActionResult> Create(AdminUserCreateInput input, CancellationToken cancellationToken)
    {
        if (!PrimaryRoles.Contains(input.Role))
            ModelState.AddModelError(nameof(input.Role), "Choose Borrower, Custodian, or Administrator.");
        input.StudentNumber = Optional(input.StudentNumber);
        input.Department = Optional(input.Department);
        input.ContactNumber = Optional(input.ContactNumber);
        if (input.Role != "Borrower" && (input.StudentNumber is not null || input.Department is not null))
            ModelState.AddModelError(nameof(input.StudentNumber), "Student ID Number and department apply to borrowers only.");
        var email = input.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
            ModelState.AddModelError(nameof(input.Email), "An account already uses this email address.");
        if (input.StudentNumber is not null &&
            await db.BorrowerProfiles.AnyAsync(x => x.StudentNumber == input.StudentNumber, cancellationToken))
            ModelState.AddModelError(nameof(input.StudentNumber), "This Student ID Number is already registered.");
        if (!ModelState.IsValid) return View("Create", input);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            FirstName = input.FirstName.Trim(), LastName = input.LastName.Trim(),
            PhoneNumber = input.ContactNumber,
            ApprovalStatus = AccountApprovalStatus.Approved, IsActive = true
        };
        var created = await users.CreateAsync(user, input.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
                ModelState.AddModelError(error.Code.Contains("Password") ? nameof(input.Password) : string.Empty, error.Description);
            return View("Create", input);
        }
        if (!(await users.AddToRoleAsync(user, input.Role)).Succeeded)
        {
            ModelState.AddModelError(string.Empty, "The role could not be assigned. Please try again.");
            return View("Create", input);
        }
        if (input.Role == "Borrower")
            db.BorrowerProfiles.Add(new BorrowerProfile
            {
                UserId = user.Id, StudentNumber = input.StudentNumber, Department = input.Department,
                ContactNumber = input.ContactNumber, IsEligible = true
            });
        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = users.GetUserId(User), Action = "Admin.UserCreated", EntityType = "ApplicationUser",
            EntityId = user.Id, Outcome = "Succeeded",
            AfterJson = JsonSerializer.Serialize(new { user.FirstName, user.LastName, user.Email, input.Role, input.StudentNumber, input.Department }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), CorrelationId = HttpContext.TraceIdentifier
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        TempData["AdminUsersFeedback"] = $"{user.FullName} can now sign in as {input.Role}.";
        return Redirect(input.Role == "Borrower" ? "/admin/borrowers" : "/admin/users");
    }

    /// <summary>
    /// User and Borrower Profile Management · Administrator only. An account with no borrowing or desk history is
    /// deleted outright (its audit entries are kept with the actor cleared). An account with history is deactivated
    /// instead, so borrowing records stay complete.
    /// </summary>
    [HttpPost("admin/users/{id}/delete")]
    public async Task<IActionResult> Delete(string id, string? returnUrl, CancellationToken cancellationToken)
    {
        var back = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin/users";
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try { await AcquireLockAsync("CampusGear:UserAdministration", cancellationToken); }
        catch (AdminUserEditException exception) { TempData["AdminUsersError"] = exception.Message; return Redirect(back); }

        var target = await db.Users.Include(x => x.BorrowerProfile).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (target is null) return NotFound();
        if (target.Id == users.GetUserId(User))
        {
            TempData["AdminUsersError"] = "You cannot delete your own account.";
            return Redirect(back);
        }
        var roles = await users.GetRolesAsync(target);
        if (roles.Contains("Administrator") && target.IsActive && !await db.UserRoles.AnyAsync(ur => ur.UserId != target.Id &&
                db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Administrator") &&
                db.Users.Any(u => u.Id == ur.UserId && u.IsActive && u.EmailConfirmed), cancellationToken))
        {
            TempData["AdminUsersError"] = "Keep at least one active, verified Administrator account.";
            return Redirect(back);
        }

        var hasHistory = await db.Reservations.AnyAsync(x => x.BorrowerProfile.UserId == target.Id || x.DecidedByUserId == target.Id, cancellationToken) ||
            await db.Loans.AnyAsync(x => x.ReleasedByUserId == target.Id || x.ReceivedByUserId == target.Id, cancellationToken) ||
            await db.MaintenanceCases.AnyAsync(x => x.OpenedByUserId == target.Id || x.ClosedByUserId == target.Id, cancellationToken);
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
            await db.AuditEvents.Where(x => x.ActorUserId == target.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(e => e.ActorUserId, (string?)null), cancellationToken);
            await db.EmailChallenges.Where(x => x.UserId == target.Id).ExecuteDeleteAsync(cancellationToken);
            if (target.BorrowerProfile is not null) db.BorrowerProfiles.Remove(target.BorrowerProfile);
            await db.SaveChangesAsync(cancellationToken);
            var deleted = await users.DeleteAsync(target);
            if (!deleted.Succeeded)
            {
                TempData["AdminUsersError"] = "The account could not be deleted. Please try again.";
                return Redirect(back);
            }
        }
        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = users.GetUserId(User), Action = hasHistory ? "Admin.UserDeactivated" : "Admin.UserDeleted",
            EntityType = "ApplicationUser", EntityId = id, Outcome = "Succeeded", BeforeJson = before,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), CorrelationId = HttpContext.TraceIdentifier
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        TempData["AdminUsersFeedback"] = hasHistory
            ? $"{target.FullName} has borrowing history, so the account was deactivated instead of deleted. They can no longer sign in."
            : $"{target.FullName}'s account was deleted.";
        return Redirect(back);
    }

    [HttpGet("admin/audit")]
    public async Task<IActionResult> Audit(string? q, int page = 1,
        CancellationToken cancellationToken = default)
    {
        q = SearchText(q);
        var query = db.AuditEvents.AsNoTracking();
        if (q.Length > 0)
            query = query.Where(x => x.Action.Contains(q) || x.EntityType.Contains(q) ||
                x.EntityId.Contains(q) || x.Outcome.Contains(q) ||
                x.IpAddress != null && x.IpAddress.Contains(q) ||
                x.ActorUser != null && (x.ActorUser.FullName.Contains(q) || x.ActorUser.Email!.Contains(q)));
        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)AuditPageSize));
        page = Math.Clamp(page, 1, pages);
        var events = await query.Include(x => x.ActorUser).OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id).Skip((page - 1) * AuditPageSize).Take(AuditPageSize)
            .ToListAsync(cancellationToken);
        var rows = events.Select(x => new AdminAuditRow(x.Id, Manila(x.CreatedAtUtc), x.Action,
            x.ActorUser?.FullName ?? "Unauthenticated / system", x.ActorUser?.Email,
            x.EntityType, x.EntityId, x.Outcome, x.IpAddress, x.CorrelationId,
            PrettyJson(x.BeforeJson), PrettyJson(x.AfterJson))).ToList();
        return View(new AdminAuditPage(rows, q, page, pages, total));
    }

    private async Task<IActionResult> EditViewAsync(string id, AdminUserEditInput? input,
        CancellationToken cancellationToken)
    {
        var target = await db.Users.AsNoTracking().Include(x => x.BorrowerProfile)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (target is null) return NotFound();
        var currentRoles = await users.GetRolesAsync(target);
        var currentRole = PrimaryRoles.LastOrDefault(currentRoles.Contains) ?? "Borrower";
        input ??= new AdminUserEditInput
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
        return View("Edit", new AdminUserEditPage(id, target.Email ?? string.Empty,
            target.ApprovalStatus, target.PhoneNumber, string.Join(", ", currentRoles),
            target.BorrowerProfile is not null, target.Id == users.GetUserId(User), input));
    }

    private async Task AcquireLockAsync(string resourceName, CancellationToken cancellationToken)
    {
        var result = new SqlParameter("@lockResult", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resourceName };
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @lockResult = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = 10000",
            new object[] { result, resource }, cancellationToken);
        if (result.Value is not int code || code < 0)
            throw new AdminUserEditException("Account administration is busy. Please try again.");
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
            user.FirstName, user.LastName, user.IsActive, user.ApprovalStatus, user.EmailConfirmed, Roles = roles.Order().ToArray(),
            Borrower = profile is null ? null : new
            {
                profile.StudentNumber, profile.Department, profile.IsEligible, profile.EligibilityNotes
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

public sealed record AdminUserRow(string Id, string FullName, string Email, string Role,
    bool IsActive, AccountApprovalStatus ApprovalStatus, string? StudentNumber, string? Department, bool? IsEligible,
    string RegisteredManila);
public sealed record AdminUsersPage(IReadOnlyList<AdminUserRow> Rows, string Query, string Role,
    string Status, int Page, int PageCount, int Total, int PendingTotal, string? Feedback, string? Error);
public sealed record AdminUserEditPage(string Id, string Email, AccountApprovalStatus ApprovalStatus,
    string? ContactNumber, string CurrentRole, bool HasBorrowerProfile, bool IsSelf, AdminUserEditInput Input);

public sealed class AdminUserEditInput
{
    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;
    [Required]
    public string Role { get; set; } = "Borrower";
    public bool IsActive { get; set; }
    [StringLength(64)]
    public string? StudentNumber { get; set; }
    [StringLength(160)]
    public string? Department { get; set; }
    public bool IsEligible { get; set; } = true;
    [StringLength(1000)]
    public string? EligibilityNotes { get; set; }
    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
    public string? ProfileRowVersion { get; set; }
}

public sealed class AdminUserCreateInput
{
    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string Role { get; set; } = "Borrower";
    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "Temporary password")]
    public string Password { get; set; } = string.Empty;
    [StringLength(64), Display(Name = "Student ID Number")]
    public string? StudentNumber { get; set; }
    [StringLength(160)]
    public string? Department { get; set; }
    [StringLength(32), Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }
}

public sealed record AdminAuditRow(Guid Id, string TimeManila, string Action, string ActorName,
    string? ActorEmail, string EntityType, string EntityId, string Outcome, string? IpAddress,
    string? CorrelationId, string? BeforeJson, string? AfterJson);
public sealed record AdminAuditPage(IReadOnlyList<AdminAuditRow> Rows, string Query, int Page,
    int PageCount, int Total);
