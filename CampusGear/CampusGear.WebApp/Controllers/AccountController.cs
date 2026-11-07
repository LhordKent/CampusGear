using System.Net;
using System.Security.Claims;
using CampusGear.Filters;
using CampusGear.Services;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace CampusGear.Controllers;
[Route("auth")]
[AutoValidateAntiforgeryToken]
[TypeFilter(typeof(AccountAntiforgeryRecoveryFilter))]
public sealed class AccountController(IAccountService service, IWebHostEnvironment environment, IServiceProvider services) : Controller
{
    private RequestContext Actor => new(User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    private CancellationToken Ct => HttpContext.RequestAborted;
    [AllowAnonymous, HttpGet("login")]
    public async Task<IActionResult> Login() => Map(await service.GetLoginAsync(Actor, Ct), populate: true);
    [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginInput input)
    {
        if (!ModelState.IsValid) return LoginView(input.Email);
        var result = await service.LoginAsync(Actor, new(input.Email, input.Password), Ct);
        return Map(result, () => LoginView(input.Email.Trim()));
    }
    [AllowAnonymous, HttpGet("signup")]
    public IActionResult SignUp() { PopulateFeedback(); return View(); }
    [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("signup")]
    public async Task<IActionResult> SignUp([FromForm] SignUpInput input)
    {
        if (!ModelState.IsValid) return SignUpView(input);
        return Map(await service.SignUpAsync(Actor, new(input.FirstName, input.LastName, input.StudentIdNumber, input.Email, input.MobileNumber, input.Password), Ct), () => SignUpView(input));
    }
    [AllowAnonymous, HttpGet("registration-submitted")]
    public IActionResult RegistrationSubmitted() => View();
    [AllowAnonymous, HttpGet("two-factor")]
    public async Task<IActionResult> TwoFactor() => Map(await service.GetTwoFactorAsync(Actor, Ct), populate: true);
    [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("two-factor")]
    public async Task<IActionResult> TwoFactor([FromForm] CodeInput input) => Map(await service.TwoFactorAsync(Actor, ModelState.IsValid ? input.Code : null, Ct));
    [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("resend-two-factor")]
    public async Task<IActionResult> ResendTwoFactor() => Map(await service.ResendTwoFactorAsync(Actor, Ct));
    [AllowAnonymous, HttpGet("password-reset-request")]
    public IActionResult PasswordResetRequest() { PopulateFeedback(); return View(); }
    [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("password-reset-request")]
    public async Task<IActionResult> PasswordResetRequest([FromForm] EmailInput input)
    {
        if (!ModelState.IsValid) { ViewData["Email"] = input.Email; return View(); }
        return Map(await service.PasswordResetRequestAsync(Actor, input.Email, Ct));
    }
    [AllowAnonymous, HttpGet("password-reset-code")]
    public async Task<IActionResult> PasswordResetCode() => Map(await service.GetPasswordResetCodeAsync(Actor, Ct), populate: true);
    [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("password-reset-code")]
    public async Task<IActionResult> PasswordResetCode([FromForm] CodeInput input) => Map(await service.PasswordResetCodeAsync(Actor, ModelState.IsValid ? input.Code : null, Ct));
    [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("resend-password-reset-code")]
    public async Task<IActionResult> ResendPasswordResetCode() => Map(await service.ResendPasswordResetCodeAsync(Actor, Ct));
    [AllowAnonymous, HttpGet("password-reset-new")]
    public async Task<IActionResult> PasswordResetNew() => Map(await service.GetPasswordResetNewAsync(Actor, Ct), populate: true);
    [AllowAnonymous, EnableRateLimiting("auth-post"), HttpPost("password-reset-new")]
    public async Task<IActionResult> PasswordResetNew([FromForm] NewPasswordInput input) => Map(await service.PasswordResetNewAsync(Actor, ModelState.IsValid ? input.Password : null, Ct));
    [AllowAnonymous, HttpGet("success")]
    public IActionResult Success() => View();
    [AllowAnonymous, HttpGet("access-denied")]
    public IActionResult AccessDenied() => View();
    [Authorize, EnableRateLimiting("auth-post"), HttpPost("logout")]
    public async Task<IActionResult> Logout() => Map(await service.LogoutAsync(Actor, Ct));
    [AllowAnonymous]
    [HttpGet("development-inbox")]
    public IActionResult DevelopmentInbox()
    {
        var developmentEmail = services.GetService<DevelopmentEmailSender>();
        if (!environment.IsDevelopment() ||
            HttpContext.Connection.RemoteIpAddress is not IPAddress remoteAddress ||
            !IPAddress.IsLoopback(remoteAddress) ||
            developmentEmail is null)
            return NotFound();

        Response.Headers.CacheControl = "no-store";
        return Json(developmentEmail.GetRecent());
    }
    private IActionResult Map(OperationResult<AccountFlow> result, Func<IActionResult>? render = null, bool populate = false)
    {
        var flow = result.Value ?? throw new InvalidOperationException("An account workflow must return its next step.");
        if (flow.Path is not null) return Redirect(flow.Path);
        if (flow.Transition)
        {
            if (result.Message is not null) TempData["ErrorMessage"] = result.Message;
            if (flow.Notice is not null) TempData["NoticeMessage"] = flow.Notice;
            return RedirectToAction(flow.Step.ToString());
        }
        if (populate) PopulateFeedback();
        if (result.Message is not null) ViewData["ErrorMessage"] = result.Message;
        if (flow.Notice is not null) ViewData["NoticeMessage"] = flow.Notice;
        if (result.Errors is not null) foreach (var error in result.Errors) ModelState.AddModelError(error.Field, error.Message);
        return render is not null ? render() : View(flow.Step.ToString());
    }
    private ViewResult LoginView(string? email)
    {
        ViewData["Email"] = email;
        return View("Login");
    }

    private ViewResult SignUpView(SignUpInput input)
    {
        ViewData["FirstName"] = input.FirstName;
        ViewData["LastName"] = input.LastName;
        ViewData["StudentIdNumber"] = input.StudentIdNumber;
        ViewData["Email"] = input.Email;
        ViewData["MobileNumber"] = input.MobileNumber;
        return View("SignUp");
    }


    private void PopulateFeedback()
    {
        if (TempData["ErrorMessage"] is string error) ViewData["ErrorMessage"] = error;
        if (TempData["NoticeMessage"] is string notice) ViewData["NoticeMessage"] = notice;
    }
}
