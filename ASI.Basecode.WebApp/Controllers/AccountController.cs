using AutoMapper;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
// =====================================================================
// ACCOUNT CONTROLLER  (all URLs start with /auth)
// Sign in, the emailed code for administrators (two-factor), sign out, and student sign-up.
//
// FLOW: browser form -> this controller -> AuthenticationService / RegistrationService
//       -> Identity + database. The controller only reads the result and picks the next page.
// =====================================================================

namespace ASI.Basecode.WebApp.Controllers
{
    [Route("auth")]
    [AutoValidateAntiforgeryToken]
    [TypeFilter(typeof(AccountAntiforgeryRecoveryFilter))]
    public sealed class AccountController : Mvc.ControllerBase<AccountController>
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly IRegistrationService _registrationService;

        public AccountController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IAuthenticationService authenticationService,
            IRegistrationService registrationService
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _authenticationService = authenticationService;
            _registrationService = registrationService;
        }

        // GET /auth/login: shows the sign-in page. Already signed in? Go straight to your dashboard.
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

        // POST /auth/login: checks email + password.
        // Result: signed in -> dashboard, administrator -> two-factor page (code emailed), wrong -> error again.
        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginViewModel model, CancellationToken cancellationToken)
        {
            ViewData["Email"] = model.Email;
            if (!ModelState.IsValid)
            {
                return View("~/Views/Account/Login.cshtml");
            }

            var input = new LoginInput { Email = model.Email, Password = model.Password };
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

        // GET /auth/two-factor: shows the "enter the code we emailed you" page (administrators only).
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

        // POST /auth/two-factor: checks the emailed code. Correct -> signed in and sent to the admin dashboard.
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

        // POST /auth/resend-two-factor: emails a new code.
        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("resend-two-factor")]
        public async Task<IActionResult> ResendTwoFactor(CancellationToken cancellationToken)
        {
            var result = await _authenticationService.ResendTwoFactorAsync(RequestActor, cancellationToken);
            StoreFeedback(result, result.Value!.Notice);
            return result.Value.Status == AuthenticationStatus.CredentialsRequired
                ? RedirectToAction(nameof(Login))
                : RedirectToAction(nameof(TwoFactor));
        }

        // POST /auth/logout: signs the user out and goes back to the login page.
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

        // GET /auth/signup: shows the student registration form.
        [AllowAnonymous, HttpGet("signup")]
        public IActionResult Register()
        {
            RestoreFeedback();
            return View("~/Views/Account/Register.cshtml");
        }

        // POST /auth/signup: creates the student account. An admin must approve it before they can sign in.
        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("signup")]
        public async Task<IActionResult> Register([FromForm] SignUpInput input)
        {
            if (ModelState.IsValid)
            {
                var result = await _registrationService.SignUpAsync(RequestActor, input, HttpContext.RequestAborted);
                if (result.Value!.Status == RegistrationStatus.Submitted)
                {
                    return RedirectToAction(nameof(RegistrationSubmitted));
                }

                ShowFeedback(result);
            }

            ViewData["FirstName"] = input.FirstName;
            ViewData["LastName"] = input.LastName;
            ViewData["StudentIdNumber"] = input.StudentIdNumber;
            ViewData["Email"] = input.Email;
            ViewData["Department"] = input.Department;
            ViewData["MobileNumber"] = input.MobileNumber;
            return View("~/Views/Account/Register.cshtml");
        }

        [AllowAnonymous, HttpGet("registration-submitted")]
        public IActionResult RegistrationSubmitted()
        {
            return View("~/Views/Account/RegistrationSubmitted.cshtml");
        }

        // Which dashboard each role lands on after signing in.
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
