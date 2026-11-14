using AutoMapper;
using CampusGear.Resources.Constants;
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
    public sealed class AuthenticationController : Mvc.ControllerBase<AuthenticationController>
    {
        private readonly IAuthenticationService _authenticationService;

        public AuthenticationController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IAuthenticationService authenticationService
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _authenticationService = authenticationService;
        }

        [AllowAnonymous, HttpGet("login")]
        public async Task<IActionResult> Login(CancellationToken cancellationToken)
        {
            var result = await _authenticationService.GetLoginAsync(RequestActor, cancellationToken);
            if (result.Value!.Status == AuthenticationStatus.SignedIn)
            {
                return Redirect(WorkspaceUrl(result.Value.Role));
            }

            RestoreFeedback();
            ShowFeedback(result, result.Value.Notice);
            return View("~/Views/Account/Login.cshtml");
        }

        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginInput input, CancellationToken cancellationToken)
        {
            ViewData["Email"] = input.Email;
            if (!ModelState.IsValid)
            {
                return View("~/Views/Account/Login.cshtml");
            }

            var result = await _authenticationService.LoginAsync(RequestActor, input, cancellationToken);
            if (result.Value!.Status == AuthenticationStatus.SignedIn)
            {
                return Redirect(WorkspaceUrl(result.Value.Role));
            }

            if (result.Value.Status == AuthenticationStatus.VerificationRequired)
            {
                StoreFeedback(result, result.Value.Notice);
                return RedirectToAction(nameof(TwoFactor));
            }

            ViewData["Email"] = input.Email.Trim();
            ShowFeedback(result, result.Value.Notice);
            return View("~/Views/Account/Login.cshtml");
        }

        [AllowAnonymous, HttpGet("two-factor")]
        public async Task<IActionResult> TwoFactor(CancellationToken cancellationToken)
        {
            var result = await _authenticationService.GetTwoFactorAsync(RequestActor, cancellationToken);
            if (result.Value!.Status == AuthenticationStatus.CredentialsRequired)
            {
                StoreFeedback(result);
                return RedirectToAction(nameof(Login));
            }

            RestoreFeedback();
            ShowFeedback(result, result.Value.Notice);
            return View("~/Views/Account/TwoFactor.cshtml");
        }

        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("two-factor")]
        public async Task<IActionResult> TwoFactor([FromForm] CodeInput input, CancellationToken cancellationToken)
        {
            var result = await _authenticationService.TwoFactorAsync(
                RequestActor,
                ModelState.IsValid ? input.Code : null,
                cancellationToken
            );
            if (result.Value!.Status == AuthenticationStatus.SignedIn)
            {
                return Redirect(WorkspaceUrl(result.Value.Role));
            }

            if (result.Value.Status == AuthenticationStatus.CredentialsRequired)
            {
                StoreFeedback(result);
                return RedirectToAction(nameof(Login));
            }

            ShowFeedback(result, result.Value.Notice);
            return View("~/Views/Account/TwoFactor.cshtml");
        }

        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("resend-two-factor")]
        public async Task<IActionResult> ResendTwoFactor(CancellationToken cancellationToken)
        {
            var result = await _authenticationService.ResendTwoFactorAsync(RequestActor, cancellationToken);
            StoreFeedback(result, result.Value!.Notice);
            return result.Value.Status == AuthenticationStatus.CredentialsRequired
                ? RedirectToAction(nameof(Login))
                : RedirectToAction(nameof(TwoFactor));
        }

        [Authorize, EnableRateLimiting("auth-post"), HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            await _authenticationService.LogoutAsync(RequestActor, cancellationToken);
            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous, HttpGet("access-denied")]
        public IActionResult AccessDenied()
        {
            return View("~/Views/Account/AccessDenied.cshtml");
        }

        private static string WorkspaceUrl(string? role)
        {
            return role switch
            {
                RoleNames.Administrator => "/admin/dashboard",
                RoleNames.Custodian => "/custodian/dashboard",
                RoleNames.Borrower => "/borrower/home",
                _ => "/auth/access-denied",
            };
        }
    }
}
