using System.Globalization;
using CampusGear.Data.Interfaces;
using CampusGear.Models;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
namespace CampusGear.Services;
public sealed class AccountService(IAccountRepository accounts,IAuditRepository audits,IUnitOfWork work,
 UserManager<ApplicationUser> _users,SignInManager<ApplicationUser> _signIn,IEmailChallengeService _challenges,
 IHostEnvironment _environment,IConfiguration _configuration,IAccountFlowStore flow) : IAccountService
{
    private const string PendingAdministratorUserId = "CampusGear.PendingAdministratorUserId";
    private const string PendingAdministratorStamp = "CampusGear.PendingAdministratorStamp";
    private const string PendingResetUserId = "CampusGear.PendingResetUserId";
    private const string PendingResetStamp = "CampusGear.PendingResetStamp";
    private const string AuthorizedResetUserId = "CampusGear.AuthorizedResetUserId";
    private const string AuthorizedResetStamp = "CampusGear.AuthorizedResetStamp";
    private const string AuthorizedResetExpiresAt = "CampusGear.AuthorizedResetExpiresAt";


    public async Task<OperationResult<AccountFlow>> GetLoginAsync(RequestContext actor, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        if (actor.UserId is not null)
        {
            var current = await _users.FindByIdAsync(actor.UserId!);
            if (current is { IsActive: true }) return feedback.Result(AccountStep.Authenticated, path: await WorkspaceForAsync(current));
            await _signIn.SignOutAsync();
        }

        
        return feedback.Result(AccountStep.Login);
    }

    public async Task<OperationResult<AccountFlow>> LoginAsync(RequestContext actor, LoginCommand input, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        

        ClearPendingAdministrator();

        var email = input.Email.Trim();
        var user = await _users.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            await AuditAsync(actor, "Auth.LoginPassword", "Rejected", user?.Id, ct);
            feedback.Error = "The email or password is incorrect.";
            return feedback.Result(AccountStep.Login);
        }

        if (user.ApprovalStatus != AccountApprovalStatus.Approved)
        {
            // The approval status is revealed only after the password is confirmed.
            if (await _users.IsLockedOutAsync(user) || !await _users.CheckPasswordAsync(user, input.Password))
            {
                if (!await _users.IsLockedOutAsync(user)) await _users.AccessFailedAsync(user);
                await AuditAsync(actor, "Auth.LoginPassword", "Rejected", user.Id, ct);
                feedback.Error = "The email or password is incorrect.";
                return feedback.Result(AccountStep.Login);
            }

            await _users.ResetAccessFailedCountAsync(user);
            var pending = user.ApprovalStatus == AccountApprovalStatus.PendingApproval;
            await AuditAsync(actor, "Auth.LoginPassword", pending ? "PendingApproval" : "RegistrationRejected", user.Id, ct);
            feedback.Error = pending
                ? "Your account is pending administrator approval."
                : "Your registration was not approved. Please contact the administrator.";
            return feedback.Result(AccountStep.Login);
        }

        var passwordResult = await _signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
        if (!passwordResult.Succeeded)
        {
            await AuditAsync(actor, "Auth.LoginPassword", "Rejected", user.Id, ct);
            feedback.Error = passwordResult.IsLockedOut
                ? "This account is temporarily locked. Try again later."
                : "The email or password is incorrect.";
            return feedback.Result(AccountStep.Login);
        }

        if (await _users.IsInRoleAsync(user, "Administrator") && !SkipAdministratorCode())
        {
            // CheckPasswordSignInAsync validates the password without creating an auth cookie.
            flow.SetString(PendingAdministratorUserId, user.Id);
            flow.SetString(PendingAdministratorStamp, user.SecurityStamp ?? string.Empty);
            var issued = await _challenges.IssueAsync(user, EmailChallengePurpose.AdministratorLogin,
                ct);
            if (issued.Status == ChallengeIssueStatus.DeliveryUnavailable)
            {
                await AuditAsync(actor, "Auth.LoginPassword", "CodeDeliveryFailed", user.Id, ct);
                feedback.Error = "We could not send the administrator code. Please try again.";
                return feedback.Result(AccountStep.Login);
            }

            feedback.Notice = issued.Status == ChallengeIssueStatus.Cooldown
                ? "A code was requested recently. Please wait before requesting a new one."
                : "We sent an administrator sign-in code to your email.";
            await AuditAsync(actor, "Auth.LoginPassword", "SecondFactorRequired", user.Id, ct);
            return feedback.Result(AccountStep.TwoFactor, transition: true);
        }

        await AuditAsync(actor, "Auth.Login", "Succeeded", user.Id, ct);
        await _signIn.SignInAsync(user, isPersistent: false);
        return feedback.Result(AccountStep.Authenticated, path: await WorkspaceForAsync(user));
    }

    public async Task<OperationResult<AccountFlow>> SignUpAsync(RequestContext actor, SignUpCommand input, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        

        var email = input.Email.Trim();
        var studentId = input.StudentIdNumber.Trim();
        if (await _users.FindByEmailAsync(email) is not null)
        {
            await AuditAsync(actor, "Auth.SignUp", "DuplicateEmail", null, ct);
            feedback.AddError(nameof(input.Email), "An account already uses this email address.");
        }
        if (await accounts.StudentExistsAsync(studentId, null, ct))
        {
            await AuditAsync(actor, "Auth.SignUp", "DuplicateStudentId", null, ct);
            feedback.AddError(nameof(input.StudentIdNumber), "This Student ID Number is already registered.");
        }
        if (feedback.Errors.Count > 0) return feedback.Result(AccountStep.SignUp);

        ApplicationUser user;
        try
        {
            await using var transaction = await work.BeginAsync(ct);
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                FirstName = input.FirstName.Trim(),
                LastName = input.LastName.Trim(),
                PhoneNumber = input.MobileNumber.Trim(),
                ApprovalStatus = AccountApprovalStatus.PendingApproval,
                IsActive = true
            };
            var created = await _users.CreateAsync(user, input.Password);
            if (!created.Succeeded)
            {
                foreach (var error in created.Errors)
                    feedback.AddError(nameof(input.Password), error.Description);
                return feedback.Result(AccountStep.SignUp);
            }

            var assigned = await _users.AddToRoleAsync(user, "Borrower");
            if (!assigned.Succeeded)
            {
                feedback.Error = "Account creation is unavailable. Please try again later.";
                return feedback.Result(AccountStep.SignUp);
            }

            accounts.AddProfile(new BorrowerProfile
            {
                UserId = user.Id,
                StudentNumber = studentId,
                ContactNumber = input.MobileNumber.Trim(),
                IsEligible = true
            });
            await work.SaveAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (PersistenceException exception) when (exception.Failure == PersistenceFailure.Duplicate)
        {
            // The unique indexes also protect a race between simultaneous signups.
            work.ClearTracking();
            if (exception.Constraint == "StudentNumber")
                feedback.AddError(nameof(input.StudentIdNumber), "This Student ID Number is already registered.");
            else
                feedback.AddError(nameof(input.Email), "An account already uses this email address.");
            return feedback.Result(AccountStep.SignUp);
        }

        await AuditAsync(actor, "Auth.SignUp", "CreatedPendingApproval", user.Id, ct);
        return feedback.Result(AccountStep.RegistrationSubmitted, transition: true);
    }

    public async Task<OperationResult<AccountFlow>> GetTwoFactorAsync(RequestContext actor, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        if (await PendingAdministratorAsync(feedback) is null)
            return feedback.Result(AccountStep.Login, transition: true);
        
        return feedback.Result(AccountStep.TwoFactor);
    }

    public async Task<OperationResult<AccountFlow>> TwoFactorAsync(RequestContext actor, string? code, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        var user = await PendingAdministratorAsync(feedback);
        if (user is null) return feedback.Result(AccountStep.Login, transition: true);
        var userId = user.Id;
        if (code is null) return feedback.Result(AccountStep.TwoFactor);

        var result = await _challenges.VerifyAsync(userId, EmailChallengePurpose.AdministratorLogin,
            code!, ct);
        if (result != ChallengeVerifyStatus.Verified)
        {
            await AuditAsync(actor, "Auth.AdministratorCode", result.ToString(), userId, ct);
            feedback.Error = ChallengeError(result);
            return feedback.Result(AccountStep.TwoFactor);
        }

        await accounts.ReloadUserAsync(user, ct);
        if (!await MatchesPendingAdministratorAsync(user))
        {
            ClearPendingAdministrator();
            await AuditAsync(actor, "Auth.AdministratorCode", "AccountUnavailable", userId, ct);
            return feedback.Result(AccountStep.Login, transition: true);
        }

        await AuditAsync(actor, "Auth.AdministratorCode", "Succeeded", user.Id, ct);
        ClearPendingAdministrator();
        await _signIn.SignInAsync(user, isPersistent: false);
        return feedback.Result(AccountStep.Authenticated, path: "/admin/dashboard");
    }

    public async Task<OperationResult<AccountFlow>> ResendTwoFactorAsync(RequestContext actor, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        var user = await PendingAdministratorAsync(feedback);
        if (user is null) return feedback.Result(AccountStep.Login, transition: true);

        var result = await _challenges.IssueAsync(user, EmailChallengePurpose.AdministratorLogin,
            ct);
        SetIssueFeedback(result, feedback);
        return feedback.Result(AccountStep.TwoFactor, transition: true);
    }

    public async Task<OperationResult<AccountFlow>> PasswordResetRequestAsync(RequestContext actor, string emailInput, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();


        var user = await _users.FindByEmailAsync(emailInput.Trim());
        flow.SetString(PendingResetUserId, user is { IsActive: true } ? user.Id : string.Empty);
        flow.SetString(PendingResetStamp, user is { IsActive: true } ? user.SecurityStamp ?? string.Empty : string.Empty);
        ClearAuthorizedReset();
        if (user is { IsActive: true })
        {
            var issued = await _challenges.IssueAsync(user, EmailChallengePurpose.PasswordReset,
                ct);
            await AuditAsync(actor, "Auth.PasswordResetRequest", issued.Status.ToString(), user.Id, ct);
        }

        // The response is intentionally identical for known and unknown addresses.
        feedback.Notice = "If an account uses that email, a reset code has been sent.";
        return feedback.Result(AccountStep.PasswordResetCode, transition: true);
    }

    public Task<OperationResult<AccountFlow>> GetPasswordResetCodeAsync(RequestContext actor, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        if (flow.GetString(PendingResetUserId) is null)
            return Task.FromResult(feedback.Result(AccountStep.PasswordResetRequest, transition: true));

        return Task.FromResult(feedback.Result(AccountStep.PasswordResetCode));
    }

    public async Task<OperationResult<AccountFlow>> PasswordResetCodeAsync(RequestContext actor, string? code, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        var userId = flow.GetString(PendingResetUserId);
        if (userId is null) return feedback.Result(AccountStep.PasswordResetRequest, transition: true);
        if (code is null) return feedback.Result(AccountStep.PasswordResetCode);
        if (userId.Length == 0)
        {
            await AuditAsync(actor, "Auth.PasswordResetCode", "Rejected", null, ct);
            feedback.Error = "The code is invalid or has expired.";
            return feedback.Result(AccountStep.PasswordResetCode);
        }

        var user = await _users.FindByIdAsync(userId);
        if (!MatchesPendingReset(user))
        {
            ClearPendingReset();
            feedback.Error = "This reset request is no longer valid. Request a new code.";
            await AuditAsync(actor, "Auth.PasswordResetCode", "StaleFlow", userId, ct);
            return feedback.Result(AccountStep.PasswordResetRequest, transition: true);
        }

        var result = await _challenges.VerifyAsync(userId, EmailChallengePurpose.PasswordReset,
            code!, ct);
        if (result != ChallengeVerifyStatus.Verified)
        {
            await AuditAsync(actor, "Auth.PasswordResetCode", result.ToString(), userId, ct);
            feedback.Error = ChallengeError(result);
            return feedback.Result(AccountStep.PasswordResetCode);
        }

        var exists = await accounts.ReloadUserAsync(user!, ct);
        if (!exists || !MatchesPendingReset(user))
        {
            ClearPendingReset();
            feedback.Error = "This reset request is no longer valid. Request a new code.";
            return feedback.Result(AccountStep.PasswordResetRequest, transition: true);
        }

        await AuditAsync(actor, "Auth.PasswordResetCode", "Succeeded", userId, ct);
        ClearPendingReset();
        flow.SetString(AuthorizedResetUserId, userId);
        flow.SetString(AuthorizedResetStamp, user!.SecurityStamp ?? string.Empty);
        flow.SetString(AuthorizedResetExpiresAt,
            DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        return feedback.Result(AccountStep.PasswordResetNew, transition: true);
    }

    public async Task<OperationResult<AccountFlow>> ResendPasswordResetCodeAsync(RequestContext actor, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        var userId = flow.GetString(PendingResetUserId);
        if (userId is null) return feedback.Result(AccountStep.PasswordResetRequest, transition: true);
        if (userId.Length > 0)
        {
            var user = await _users.FindByIdAsync(userId);
            if (!MatchesPendingReset(user))
            {
                ClearPendingReset();
                return feedback.Result(AccountStep.PasswordResetRequest, transition: true);
            }
            await _challenges.IssueAsync(user!, EmailChallengePurpose.PasswordReset,
                ct);
        }
        feedback.Notice = "If an account uses that email, a reset code has been sent.";
        return feedback.Result(AccountStep.PasswordResetCode, transition: true);
    }

    public async Task<OperationResult<AccountFlow>> GetPasswordResetNewAsync(RequestContext actor, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        if (await AuthorizedResetUserAsync(feedback) is null)
            return feedback.Result(AccountStep.PasswordResetRequest, transition: true);
        
        return feedback.Result(AccountStep.PasswordResetNew);
    }

    public async Task<OperationResult<AccountFlow>> PasswordResetNewAsync(RequestContext actor, string? password, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        var user = await AuthorizedResetUserAsync(feedback);
        if (user is null) return feedback.Result(AccountStep.PasswordResetRequest, transition: true);
        if (password is null) return feedback.Result(AccountStep.PasswordResetNew);

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, password!);
        if (!result.Succeeded)
        {
            await AuditAsync(actor, "Auth.PasswordReset", "Rejected", user.Id, ct);
            foreach (var error in result.Errors)
                    feedback.AddError("Password", error.Description);
            return feedback.Result(AccountStep.PasswordResetNew);
        }

        await AuditAsync(actor, "Auth.PasswordReset", "Succeeded", user.Id, ct);
        ClearAuthorizedReset();
        await _signIn.SignOutAsync();
        return feedback.Result(AccountStep.Success, transition: true);
    }

    public async Task<OperationResult<AccountFlow>> LogoutAsync(RequestContext actor, CancellationToken ct = default)
    {
        var feedback = new FlowFeedback();
        await AuditAsync(actor, "Auth.Logout", "Succeeded", actor.UserId, ct);
        await _signIn.SignOutAsync();
        flow.Clear();
        return feedback.Result(AccountStep.Login, transition: true);
    }
    private async Task<string> WorkspaceForAsync(ApplicationUser user)
    {
        if (await _users.IsInRoleAsync(user, "Administrator")) return "/admin/dashboard";
        if (await _users.IsInRoleAsync(user, "Custodian")) return "/custodian/dashboard";
        if (await _users.IsInRoleAsync(user, "Borrower")) return "/borrower/home";
        return "/auth/access-denied";
    }


    private static void SetIssueFeedback(ChallengeIssueResult result, FlowFeedback feedback)
    {
        switch (result.Status)
        {
            case ChallengeIssueStatus.Sent:
                feedback.Notice = "A new code was sent to your email address.";
                break;
            case ChallengeIssueStatus.Cooldown:
                feedback.Notice = "Please wait before requesting another code.";
                break;
            default:
                feedback.Error = "We could not send a code. Please try again.";
                break;
        }
    }

    private static string ChallengeError(ChallengeVerifyStatus status) => status switch
    {
        ChallengeVerifyStatus.Expired => "The code expired. Request a new one.",
        ChallengeVerifyStatus.TooManyAttempts => "Too many incorrect attempts. Request a new code.",
        _ => "The code is invalid or has already been used."
    };

    private async Task<ApplicationUser?> PendingAdministratorAsync(FlowFeedback feedback)
    {
        var id = flow.GetString(PendingAdministratorUserId);
        var user = id is null ? null : await _users.FindByIdAsync(id);
        if (user is not null && await MatchesPendingAdministratorAsync(user)) return user;
        ClearPendingAdministrator();
        if (id is not null) feedback.Error = "This sign-in request is no longer valid. Sign in again.";
        return null;
    }

    private async Task<bool> MatchesPendingAdministratorAsync(ApplicationUser user)
    {
        var stamp = flow.GetString(PendingAdministratorStamp);
        return user.IsActive && user.EmailConfirmed && !string.IsNullOrEmpty(stamp) &&
               user.Id == flow.GetString(PendingAdministratorUserId) &&
               string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal) &&
               await _users.IsInRoleAsync(user, "Administrator");
    }

    // Local Development without SMTP cannot deliver the administrator code, so the password alone is accepted.
    private bool SkipAdministratorCode() =>
        _environment.IsDevelopment() &&
        (_configuration?["Email:Provider"] ?? "Smtp").Equals("Smtp", StringComparison.OrdinalIgnoreCase) &&
        string.IsNullOrWhiteSpace(_configuration?["Email:Smtp:Host"]);

    private bool MatchesPendingReset(ApplicationUser? user)
    {
        var stamp = flow.GetString(PendingResetStamp);
        return user is { IsActive: true } && !string.IsNullOrEmpty(stamp) &&
               user.Id == flow.GetString(PendingResetUserId) &&
               string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal);
    }

    private async Task<ApplicationUser?> AuthorizedResetUserAsync(FlowFeedback feedback)
    {
        var id = flow.GetString(AuthorizedResetUserId);
        var stamp = flow.GetString(AuthorizedResetStamp);
        var expiry = flow.GetString(AuthorizedResetExpiresAt);
        if (id is null || string.IsNullOrEmpty(stamp) ||
            !long.TryParse(expiry, NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAt) ||
            expiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            ClearAuthorizedReset();
            if (id is not null) feedback.Error = "Your password reset permission expired. Request a new code.";
            return null;
        }

        var user = await _users.FindByIdAsync(id);
        if (user is { IsActive: true } && string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal))
            return user;
        ClearAuthorizedReset();
        feedback.Error = "This reset request is no longer valid. Request a new code.";
        return null;
    }

    private void ClearPendingAdministrator()
    {
        flow.Remove(PendingAdministratorUserId);
        flow.Remove(PendingAdministratorStamp);
    }

    private void ClearPendingReset()
    {
        flow.Remove(PendingResetUserId);
        flow.Remove(PendingResetStamp);
    }

    private void ClearAuthorizedReset()
    {
        flow.Remove(AuthorizedResetUserId);
        flow.Remove(AuthorizedResetStamp);
        flow.Remove(AuthorizedResetExpiresAt);
    }

    private async Task AuditAsync(RequestContext actor, string action, string outcome, string? userId, CancellationToken ct)
    {
        audits.Add(new AuditEvent
        {
            ActorUserId = actor.UserId,
            Action = action,
            EntityType = "ApplicationUser",
            EntityId = userId ?? "unknown",
            Outcome = outcome,
            IpAddress = actor.IpAddress,
            CorrelationId = actor.CorrelationId
        });
        await work.SaveAsync(ct);
    }
 private sealed class FlowFeedback
 {
 public string? Error {get;set;}
 public string? Notice {get;set;}
 public List<FieldError> Errors {get;}=[];
 public void AddError(string field,string message)=>Errors.Add(new(field,message));
 public OperationResult<AccountFlow> Result(AccountStep step,bool transition=false,string?path=null)=>
 new(Error is null && Errors.Count==0?OperationOutcome.Success:OperationOutcome.Invalid,new(step,transition,path,Notice),Error,Errors);
 }
}
