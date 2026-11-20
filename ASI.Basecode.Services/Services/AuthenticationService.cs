using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

// =====================================================================
// AUTHENTICATION SERVICE  -  sign in and sign out.
//   Students and custodians: email + password -> signed in.
//   Administrators: email + password -> a code is emailed -> enter the code -> signed in.
// Failed passwords count toward a lockout. Every attempt is written to the audit log.
// =====================================================================
namespace ASI.Basecode.Services.Services
{
    public sealed class AuthenticationService : ServiceBase, IAuthenticationService
    {
        private readonly IUserRepository _accountRepository;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IEmailChallengeService _emailChallengeService;
        private readonly IHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IAccountFlowStore _flow;
        private readonly IAuditService _audit;

        public AuthenticationService(
            IUserRepository accounts,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IEmailChallengeService emailChallengeService,
            IHostEnvironment environment,
            IConfiguration configuration,
            IAccountFlowStore flow,
            IAuditService audit
        )
        {
            _accountRepository = accounts;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailChallengeService = emailChallengeService;
            _environment = environment;
            _configuration = configuration;
            _flow = flow;
            _audit = audit;
        }

        // The login page: if you're already signed in, it says where to send you.
        public async Task<OperationResult<AuthenticationResult>> GetLoginAsync(RequestContext actor, CancellationToken ct = default)
        {
            var feedback = new AccountFeedback();
            if (actor.UserId is not null)
            {
                var current = await _userManager.FindByIdAsync(actor.UserId!);
                if (current is { IsActive: true })
                {
                    return feedback.Result(
                        new AuthenticationResult(AuthenticationStatus.SignedIn, await GetWorkspaceRoleAsync(current), feedback.Notice)
                    );
                }

                await _signInManager.SignOutAsync();
            }

            return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
        }

        // WBS 4.00 | Resolve the account by email, then check its state before issuing a cookie or requesting a code.
        public async Task<OperationResult<AuthenticationResult>> LoginAsync(
            RequestContext actor,
            LoginInput input,
            CancellationToken ct = default
        )
        {
            var feedback = new AccountFeedback();
            ClearPendingAdministrator();
            var email = input.Email.Trim();
            // Unknown or inactive account: refuse (without saying which).
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null || !user.IsActive)
            {
                await _audit.RecordAccountAsync(actor, "Auth.LoginPassword", "Rejected", user?.Id, ct);
                feedback.Error = Errors.InvalidCredentials;
                return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
            }

            // Not approved yet (or rejected): tell them only AFTER the password is right.
            if (user.ApprovalStatus != AccountApprovalStatus.Approved)
            {
                // The approval status is revealed only after the password is confirmed.
                if (await _userManager.IsLockedOutAsync(user) || !await _userManager.CheckPasswordAsync(user, input.Password))
                {
                    if (!await _userManager.IsLockedOutAsync(user))
                    {
                        await _userManager.AccessFailedAsync(user);
                    }

                    await _audit.RecordAccountAsync(actor, "Auth.LoginPassword", "Rejected", user.Id, ct);
                    feedback.Error = Errors.InvalidCredentials;
                    return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
                }

                await _userManager.ResetAccessFailedCountAsync(user);
                var pending = user.ApprovalStatus == AccountApprovalStatus.PendingApproval;
                await _audit.RecordAccountAsync(
                    actor,
                    "Auth.LoginPassword",
                    pending ? "PendingApproval" : "RegistrationRejected",
                    user.Id,
                    ct
                );
                feedback.Error = pending ? Errors.PendingApproval : Errors.RegistrationRejected;
                return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
            }

            // WBS 5.00 | Identity counts failed passwords for approved accounts and reports when the account locks.
            var passwordResult = await _signInManager.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
            if (!passwordResult.Succeeded)
            {
                await _audit.RecordAccountAsync(actor, "Auth.LoginPassword", "Rejected", user.Id, ct);
                feedback.Error = passwordResult.IsLockedOut
                    ? Errors.AccountLocked
                    : Errors.InvalidCredentials;
                return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
            }

            // Administrators need a second step: email them a code.
            if (await _userManager.IsInRoleAsync(user, RoleNames.Administrator) && !SkipAdministratorCode())
            {
                // WBS 4.00 / WBS 5.00 | The password check leaves cookie issuance to the verified code step.
                _flow.PendingAdministrator = new(user.Id, user.SecurityStamp ?? string.Empty);
                var issued = await _emailChallengeService.IssueAsync(user, EmailChallengePurpose.AdministratorLogin, ct);
                if (issued.Status == ChallengeIssueStatus.DeliveryUnavailable)
                {
                    await _audit.RecordAccountAsync(actor, "Auth.LoginPassword", "CodeDeliveryFailed", user.Id, ct);
                    feedback.Error = Errors.AdministratorCodeDeliveryFailed;
                    return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
                }

                feedback.Notice =
                    issued.Status == ChallengeIssueStatus.Cooldown
                        ? Errors.CodeCooldown
                        : Common.AdministratorCodeSent;
                await _audit.RecordAccountAsync(actor, "Auth.LoginPassword", "SecondFactorRequired", user.Id, ct);
                return feedback.Result(new AuthenticationResult(AuthenticationStatus.VerificationRequired, Notice: feedback.Notice));
            }

            // Everyone else is signed in now.
            await _audit.RecordAccountAsync(actor, "Auth.Login", "Succeeded", user.Id, ct);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return feedback.Result(
                new AuthenticationResult(AuthenticationStatus.SignedIn, await GetWorkspaceRoleAsync(user), feedback.Notice)
            );
        }

        // The "enter your code" page (only valid right after step 1).
        public async Task<OperationResult<AuthenticationResult>> GetTwoFactorAsync(RequestContext actor, CancellationToken ct = default)
        {
            var feedback = new AccountFeedback();
            if (await PendingAdministratorAsync(feedback) is null)
            {
                return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
            }

            return feedback.Result(new AuthenticationResult(AuthenticationStatus.VerificationRequired, Notice: feedback.Notice));
        }

        // WBS 4.00 | Bind verification to the pending account and recheck that account before signing in.
        public async Task<OperationResult<AuthenticationResult>> TwoFactorAsync(
            RequestContext actor,
            string? code,
            CancellationToken ct = default
        )
        {
            var feedback = new AccountFeedback();
            var user = await PendingAdministratorAsync(feedback);
            if (user is null)
            {
                return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
            }

            var userId = user.Id;
            if (code is null)
            {
                return feedback.Result(new AuthenticationResult(AuthenticationStatus.VerificationRequired, Notice: feedback.Notice));
            }

            var result = await _emailChallengeService.VerifyAsync(userId, EmailChallengePurpose.AdministratorLogin, code!, ct);
            if (result != ChallengeVerifyStatus.Verified)
            {
                await _audit.RecordAccountAsync(actor, "Auth.AdministratorCode", result.ToString(), userId, ct);
                feedback.Error = ChallengeError(result);
                return feedback.Result(new AuthenticationResult(AuthenticationStatus.VerificationRequired, Notice: feedback.Notice));
            }

            await _accountRepository.ReloadUserAsync(user, ct);
            if (!await MatchesPendingAdministratorAsync(user))
            {
                ClearPendingAdministrator();
                await _audit.RecordAccountAsync(actor, "Auth.AdministratorCode", "AccountUnavailable", userId, ct);
                return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
            }

            await _audit.RecordAccountAsync(actor, "Auth.AdministratorCode", "Succeeded", user.Id, ct);
            ClearPendingAdministrator();
            await _signInManager.SignInAsync(user, isPersistent: false);
            return feedback.Result(new AuthenticationResult(AuthenticationStatus.SignedIn, RoleNames.Administrator, feedback.Notice));
        }

        // Emails a new code (there is a short wait between resends).
        public async Task<OperationResult<AuthenticationResult>> ResendTwoFactorAsync(RequestContext actor, CancellationToken ct = default)
        {
            var feedback = new AccountFeedback();
            var user = await PendingAdministratorAsync(feedback);
            if (user is null)
            {
                return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
            }

            var result = await _emailChallengeService.IssueAsync(user, EmailChallengePurpose.AdministratorLogin, ct);
            SetIssueFeedback(result, feedback);
            return feedback.Result(new AuthenticationResult(AuthenticationStatus.VerificationRequired, Notice: feedback.Notice));
        }

        // WBS 4.00 / WBS 5.00 | End the Identity sign-in and clear any unfinished account flows for this browser.
        public async Task<OperationResult<AuthenticationResult>> LogoutAsync(RequestContext actor, CancellationToken ct = default)
        {
            var feedback = new AccountFeedback();
            await _audit.RecordAccountAsync(actor, "Auth.Logout", "Succeeded", actor.UserId, ct);
            await _signInManager.SignOutAsync();
            _flow.Clear();
            return feedback.Result(new AuthenticationResult(AuthenticationStatus.CredentialsRequired, Notice: feedback.Notice));
        }

        // WBS 4.00 / WBS 45.00 | Resolve the landing-page role from Identity membership.
        private async Task<string> GetWorkspaceRoleAsync(User user)
        {
            if (await _userManager.IsInRoleAsync(user, RoleNames.Administrator))
            {
                return RoleNames.Administrator;
            }

            if (await _userManager.IsInRoleAsync(user, RoleNames.Custodian))
            {
                return RoleNames.Custodian;
            }

            if (await _userManager.IsInRoleAsync(user, RoleNames.Borrower))
            {
                return RoleNames.Borrower;
            }

            return "";
        }

        private static void SetIssueFeedback(ChallengeIssueResult result, AccountFeedback feedback)
        {
            switch (result.Status)
            {
                case ChallengeIssueStatus.Sent:
                    feedback.Notice = Common.CodeResent;
                    break;
                case ChallengeIssueStatus.Cooldown:
                    feedback.Notice = Errors.ResendCooldown;
                    break;
                default:
                    feedback.Error = Errors.CodeDeliveryFailed;
                    break;
            }
        }

        private static string ChallengeError(ChallengeVerifyStatus status)
        {
            return status switch
            {
                ChallengeVerifyStatus.Expired => Errors.CodeExpired,
                ChallengeVerifyStatus.TooManyAttempts => Errors.CodeAttemptsExceeded,
                _ => Errors.AuthenticationCodeInvalid,
            };
        }

        private async Task<User?> PendingAdministratorAsync(AccountFeedback feedback)
        {
            var id = _flow.PendingAdministrator?.UserId;
            var user = id is null ? null : await _userManager.FindByIdAsync(id);
            if (user is not null && await MatchesPendingAdministratorAsync(user))
            {
                return user;
            }

            ClearPendingAdministrator();
            if (id is not null)
            {
                feedback.Error = Errors.SignInExpired;
            }

            return null;
        }

        // WBS 4.00 / WBS 5.00 | A changed security stamp invalidates a pending administrator sign-in.
        private async Task<bool> MatchesPendingAdministratorAsync(User user)
        {
            var stamp = _flow.PendingAdministrator?.SecurityStamp;
            return user.IsActive
                && user.EmailConfirmed
                && !string.IsNullOrEmpty(stamp)
                && user.Id == _flow.PendingAdministrator?.UserId
                && string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal)
                && await _userManager.IsInRoleAsync(user, RoleNames.Administrator);
        }

        // Local Development without SMTP cannot deliver the administrator code, so the password alone is accepted.
        private bool SkipAdministratorCode()
        {
            return _environment.IsDevelopment()
                && (_configuration?["Email:Provider"] ?? "Smtp").Equals("Smtp", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(_configuration?["Email:Smtp:Host"]);
        }

        private void ClearPendingAdministrator()
        {
            _flow.PendingAdministrator = null;
        }
    }
}
