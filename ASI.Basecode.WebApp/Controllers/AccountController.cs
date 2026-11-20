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

        // WBS 4.00 | A signed-in visitor resumes their workspace when opening the login page.
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

        // WBS 4.00 / WBS 5.00 | Bind the sign-in form here; Identity checks credentials and lockout in AuthenticationService.
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

        // WBS 4.00 | Render the code form only while the pending sign-in is valid.
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

        // WBS 4.00 | Bind Code from TwoFactor.cshtml; the service can finish sign-in after verification.
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

        // WBS 4.00 | Resend uses the pending account in the flow session rather than a posted account ID.
        [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("resend-two-factor")]
        public async Task<IActionResult> ResendTwoFactor(CancellationToken cancellationToken)
        {
            var result = await _authenticationService.ResendTwoFactorAsync(RequestActor, cancellationToken);
            StoreFeedback(result, result.Value!.Notice);
            return result.Value.Status == AuthenticationStatus.CredentialsRequired
                ? RedirectToAction(nameof(Login))
                : RedirectToAction(nameof(TwoFactor));
        }

        // WBS 4.00 | Resolve the account from the authenticated request; both workspace forms use this action.
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

        // WBS 4.00 / WBS 45.00 | The signed-in role determines the landing page.
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
