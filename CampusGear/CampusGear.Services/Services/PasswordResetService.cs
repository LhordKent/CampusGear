using CampusGear.Data.Interfaces;
using CampusGear.Data.Models;
using CampusGear.Resources.Constants;
using CampusGear.Resources.Messages;
using CampusGear.Services.Helpers;
using CampusGear.Services.Interfaces;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;

namespace CampusGear.Services.Services
{
    public sealed class PasswordResetService : IPasswordResetService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailChallengeService _emailChallengeService;
        private readonly IAccountFlowStore _flow;
        private readonly IAuditService _audit;

        public PasswordResetService(
            IAccountRepository accounts,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailChallengeService emailChallengeService,
            IAccountFlowStore flow,
            IAuditService audit
        )
        {
            _accountRepository = accounts;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailChallengeService = emailChallengeService;
            _flow = flow;
            _audit = audit;
        }

        public async Task<OperationResult<PasswordResetResult>> PasswordResetRequestAsync(
            RequestContext actor,
            string emailInput,
            CancellationToken ct = default
        )
        {
            var feedback = new AccountFeedback();
            var user = await _userManager.FindByEmailAsync(emailInput.Trim());
            _flow.PendingPasswordReset = new(
                user is { IsActive: true } ? user.Id : string.Empty,
                user is { IsActive: true } ? user.SecurityStamp ?? string.Empty : string.Empty
            );
            ClearAuthorizedReset();
            if (user is { IsActive: true })
            {
                var issued = await _emailChallengeService.IssueAsync(user, EmailChallengePurpose.PasswordReset, ct);
                await _audit.RecordAccountAsync(actor, "Auth.PasswordResetRequest", issued.Status.ToString(), user.Id, ct);
            }

            // The response is intentionally identical for known and unknown addresses.
            feedback.Notice = PasswordResetMessages.RequestAccepted;
            return feedback.Result(new PasswordResetResult(PasswordResetStatus.CodeRequired, feedback.Notice));
        }

        public Task<OperationResult<PasswordResetResult>> GetPasswordResetCodeAsync(RequestContext actor, CancellationToken ct = default)
        {
            var feedback = new AccountFeedback();
            if (_flow.PendingPasswordReset?.UserId is null)
            {
                return Task.FromResult(feedback.Result(new PasswordResetResult(PasswordResetStatus.RequestRequired, feedback.Notice)));
            }

            return Task.FromResult(feedback.Result(new PasswordResetResult(PasswordResetStatus.CodeRequired, feedback.Notice)));
        }

        public async Task<OperationResult<PasswordResetResult>> PasswordResetCodeAsync(
            RequestContext actor,
            string? code,
            CancellationToken ct = default
        )
        {
            var feedback = new AccountFeedback();
            var userId = _flow.PendingPasswordReset?.UserId;
            if (userId is null)
            {
                return feedback.Result(new PasswordResetResult(PasswordResetStatus.RequestRequired, feedback.Notice));
            }

            if (code is null)
            {
                return feedback.Result(new PasswordResetResult(PasswordResetStatus.CodeRequired, feedback.Notice));
            }

            if (userId.Length == 0)
            {
                await _audit.RecordAccountAsync(actor, "Auth.PasswordResetCode", "Rejected", null, ct);
                feedback.Error = PasswordResetMessages.CodeInvalid;
                return feedback.Result(new PasswordResetResult(PasswordResetStatus.CodeRequired, feedback.Notice));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (!MatchesPendingReset(user))
            {
                ClearPendingReset();
                feedback.Error = PasswordResetMessages.ResetRequestExpired;
                await _audit.RecordAccountAsync(actor, "Auth.PasswordResetCode", "StaleFlow", userId, ct);
                return feedback.Result(new PasswordResetResult(PasswordResetStatus.RequestRequired, feedback.Notice));
            }

            var result = await _emailChallengeService.VerifyAsync(userId, EmailChallengePurpose.PasswordReset, code!, ct);
            if (result != ChallengeVerifyStatus.Verified)
            {
                await _audit.RecordAccountAsync(actor, "Auth.PasswordResetCode", result.ToString(), userId, ct);
                feedback.Error = ChallengeError(result);
                return feedback.Result(new PasswordResetResult(PasswordResetStatus.CodeRequired, feedback.Notice));
            }

            var exists = await _accountRepository.ReloadUserAsync(user!, ct);
            if (!exists || !MatchesPendingReset(user))
            {
                ClearPendingReset();
                feedback.Error = PasswordResetMessages.ResetRequestExpired;
                return feedback.Result(new PasswordResetResult(PasswordResetStatus.RequestRequired, feedback.Notice));
            }

            await _audit.RecordAccountAsync(actor, "Auth.PasswordResetCode", "Succeeded", userId, ct);
            ClearPendingReset();
            _flow.AuthorizedPasswordReset = new(
                userId,
                user!.SecurityStamp ?? string.Empty,
                DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()
            );
            return feedback.Result(new PasswordResetResult(PasswordResetStatus.PasswordRequired, feedback.Notice));
        }

        public async Task<OperationResult<PasswordResetResult>> ResendPasswordResetCodeAsync(
            RequestContext actor,
            CancellationToken ct = default
        )
        {
            var feedback = new AccountFeedback();
            var userId = _flow.PendingPasswordReset?.UserId;
            if (userId is null)
            {
                return feedback.Result(new PasswordResetResult(PasswordResetStatus.RequestRequired, feedback.Notice));
            }

            if (userId.Length > 0)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (!MatchesPendingReset(user))
                {
                    ClearPendingReset();
                    return feedback.Result(new PasswordResetResult(PasswordResetStatus.RequestRequired, feedback.Notice));
                }

                await _emailChallengeService.IssueAsync(user!, EmailChallengePurpose.PasswordReset, ct);
            }

            feedback.Notice = PasswordResetMessages.RequestAccepted;
            return feedback.Result(new PasswordResetResult(PasswordResetStatus.CodeRequired, feedback.Notice));
        }

        public async Task<OperationResult<PasswordResetResult>> GetPasswordResetNewAsync(
            RequestContext actor,
            CancellationToken ct = default
        )
        {
            var feedback = new AccountFeedback();
            if (await AuthorizedResetUserAsync(feedback) is null)
            {
                return feedback.Result(new PasswordResetResult(PasswordResetStatus.RequestRequired, feedback.Notice));
            }

            return feedback.Result(new PasswordResetResult(PasswordResetStatus.PasswordRequired, feedback.Notice));
        }

        public async Task<OperationResult<PasswordResetResult>> PasswordResetNewAsync(
            RequestContext actor,
            string? password,
            CancellationToken ct = default
        )
        {
            var feedback = new AccountFeedback();
            var user = await AuthorizedResetUserAsync(feedback);
            if (user is null)
            {
                return feedback.Result(new PasswordResetResult(PasswordResetStatus.RequestRequired, feedback.Notice));
            }

            if (password is null)
            {
                return feedback.Result(new PasswordResetResult(PasswordResetStatus.PasswordRequired, feedback.Notice));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, password!);
            if (!result.Succeeded)
            {
                await _audit.RecordAccountAsync(actor, "Auth.PasswordReset", "Rejected", user.Id, ct);
                foreach (var error in result.Errors)
                {
                    feedback.AddError("Password", error.Description);
                }

                return feedback.Result(new PasswordResetResult(PasswordResetStatus.PasswordRequired, feedback.Notice));
            }

            await _audit.RecordAccountAsync(actor, "Auth.PasswordReset", "Succeeded", user.Id, ct);
            ClearAuthorizedReset();
            await _signInManager.SignOutAsync();
            return feedback.Result(new PasswordResetResult(PasswordResetStatus.Completed, feedback.Notice));
        }

        private static string ChallengeError(ChallengeVerifyStatus status)
        {
            return status switch
            {
                ChallengeVerifyStatus.Expired => PasswordResetMessages.CodeExpired,
                ChallengeVerifyStatus.TooManyAttempts => PasswordResetMessages.CodeAttemptsExceeded,
                _ => PasswordResetMessages.CodeAlreadyUsed,
            };
        }

        private bool MatchesPendingReset(ApplicationUser? user)
        {
            var stamp = _flow.PendingPasswordReset?.SecurityStamp;
            return user is { IsActive: true }
                && !string.IsNullOrEmpty(stamp)
                && user.Id == _flow.PendingPasswordReset?.UserId
                && string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal);
        }

        private async Task<ApplicationUser?> AuthorizedResetUserAsync(AccountFeedback feedback)
        {
            var id = _flow.AuthorizedPasswordReset?.UserId;
            var stamp = _flow.AuthorizedPasswordReset?.SecurityStamp;
            var expiresAt = _flow.AuthorizedPasswordReset?.ExpiresAtUnixSeconds ?? 0;
            if (id is null || string.IsNullOrEmpty(stamp) || expiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                ClearAuthorizedReset();
                if (id is not null)
                {
                    feedback.Error = PasswordResetMessages.ResetAuthorizationExpired;
                }

                return null;
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user is { IsActive: true } && string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal))
            {
                return user;
            }

            ClearAuthorizedReset();
            feedback.Error = PasswordResetMessages.ResetRequestExpired;
            return null;
        }

        private void ClearPendingReset()
        {
            _flow.PendingPasswordReset = null;
        }

        private void ClearAuthorizedReset()
        {
            _flow.AuthorizedPasswordReset = null;
        }
    }
}
