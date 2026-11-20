using AutoMapper;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

// =====================================================================
// PASSWORD RESET CONTROLLER  (URLs start with /auth)
// "Forgot password" in 3 steps:  1) enter email -> 2) enter the emailed code -> 3) choose a new password.
// FLOW: each step -> this controller -> PasswordResetService -> database / email sender.
// =====================================================================
namespace ASI.Basecode.WebApp.Controllers
{
    [Route("auth")]
    [AutoValidateAntiforgeryToken]
    [TypeFilter(typeof(AccountAntiforgeryRecoveryFilter))]
    public sealed class PasswordResetController : Mvc.ControllerBase<PasswordResetController>
    {
        private readonly IPasswordResetService _passwordResetService;

        public PasswordResetController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IPasswordResetService passwordResetService
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _passwordResetService = passwordResetService;
        }

        // Step 1 (GET): the "enter your email" page.
        [AllowAnonymous, HttpGet("password-reset-request")]
        public IActionResult PasswordResetRequest()
        {
            RestoreFeedback();
            return View("~/Views/Account/PasswordResetRequest.cshtml");
        }

        // Step 1 (POST): emails a reset code if the account exists (the page never says whether it does).
        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("password-reset-request")]
        public async Task<IActionResult> PasswordResetRequest([FromForm] EmailInput input, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                ViewData["Email"] = input.Email;
                return View("~/Views/Account/PasswordResetRequest.cshtml");
            }

            var result = await _passwordResetService.PasswordResetRequestAsync(RequestActor, input.Email, cancellationToken);
            StoreFeedback(result, result.Value!.Notice);
            return RedirectToAction(nameof(PasswordResetCode));
        }

        // Step 2 (GET): the "enter the code" page.
        [AllowAnonymous, HttpGet("password-reset-code")]
        public async Task<IActionResult> PasswordResetCode(CancellationToken cancellationToken)
        {
            var result = await _passwordResetService.GetPasswordResetCodeAsync(RequestActor, cancellationToken);
            if (result.Value!.Status == PasswordResetStatus.RequestRequired)
            {
                return RedirectToAction(nameof(PasswordResetRequest));
            }

            RestoreFeedback();
            ShowFeedback(result, result.Value.Notice);
            return View("~/Views/Account/PasswordResetCode.cshtml");
        }

        // Step 2 (POST): checks the code.
        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("password-reset-code")]
        public async Task<IActionResult> PasswordResetCode([FromForm] CodeInput input, CancellationToken cancellationToken)
        {
            var result = await _passwordResetService.PasswordResetCodeAsync(
                RequestActor,
                ModelState.IsValid ? input.Code : null,
                cancellationToken
            );
            if (result.Value!.Status == PasswordResetStatus.PasswordRequired)
            {
                return RedirectToAction(nameof(PasswordResetNew));
            }

            if (result.Value.Status == PasswordResetStatus.RequestRequired)
            {
                StoreFeedback(result);
                return RedirectToAction(nameof(PasswordResetRequest));
            }

            ShowFeedback(result, result.Value.Notice);
            return View("~/Views/Account/PasswordResetCode.cshtml");
        }

        // Sends a new code.
        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("resend-password-reset-code")]
        public async Task<IActionResult> ResendPasswordResetCode(CancellationToken cancellationToken)
        {
            var result = await _passwordResetService.ResendPasswordResetCodeAsync(RequestActor, cancellationToken);
            StoreFeedback(result, result.Value!.Notice);
            return result.Value.Status == PasswordResetStatus.RequestRequired
                ? RedirectToAction(nameof(PasswordResetRequest))
                : RedirectToAction(nameof(PasswordResetCode));
        }

        // Step 3 (GET): the "choose a new password" page (only after a correct code).
        [AllowAnonymous, HttpGet("password-reset-new")]
        public async Task<IActionResult> PasswordResetNew(CancellationToken cancellationToken)
        {
            var result = await _passwordResetService.GetPasswordResetNewAsync(RequestActor, cancellationToken);
            if (result.Value!.Status == PasswordResetStatus.RequestRequired)
            {
                StoreFeedback(result);
                return RedirectToAction(nameof(PasswordResetRequest));
            }

            RestoreFeedback();
            ShowFeedback(result, result.Value.Notice);
            return View("~/Views/Account/PasswordResetNew.cshtml");
        }

        // Step 3 (POST): saves the new password.
        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("password-reset-new")]
        public async Task<IActionResult> PasswordResetNew([FromForm] NewPasswordInput input, CancellationToken cancellationToken)
        {
            var result = await _passwordResetService.PasswordResetNewAsync(
                RequestActor,
                ModelState.IsValid ? input.Password : null,
                cancellationToken
            );
            if (result.Value!.Status == PasswordResetStatus.Completed)
            {
                return RedirectToAction(nameof(Success));
            }

            if (result.Value.Status == PasswordResetStatus.RequestRequired)
            {
                StoreFeedback(result);
                return RedirectToAction(nameof(PasswordResetRequest));
            }

            ShowFeedback(result, result.Value.Notice);
            return View("~/Views/Account/PasswordResetNew.cshtml");
        }

        // "Password changed" page.
        [AllowAnonymous, HttpGet("success")]
        public IActionResult Success() => View("~/Views/Account/Success.cshtml");
    }
}
