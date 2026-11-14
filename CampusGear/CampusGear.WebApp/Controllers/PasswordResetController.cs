using AutoMapper;
using CampusGear.Services.Interfaces;
using CampusGear.Services.ServiceModels;
using CampusGear.WebApp.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CampusGear.WebApp.Controllers
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

        [AllowAnonymous, HttpGet("password-reset-request")]
        public IActionResult PasswordResetRequest()
        {
            RestoreFeedback();
            return View("~/Views/Account/PasswordResetRequest.cshtml");
        }

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

        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("resend-password-reset-code")]
        public async Task<IActionResult> ResendPasswordResetCode(CancellationToken cancellationToken)
        {
            var result = await _passwordResetService.ResendPasswordResetCodeAsync(RequestActor, cancellationToken);
            StoreFeedback(result, result.Value!.Notice);
            return result.Value.Status == PasswordResetStatus.RequestRequired
                ? RedirectToAction(nameof(PasswordResetRequest))
                : RedirectToAction(nameof(PasswordResetCode));
        }

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

        [AllowAnonymous, HttpGet("success")]
        public IActionResult Success() => View("~/Views/Account/Success.cshtml");
    }
}
