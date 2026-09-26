using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using CampusGear.Data;
using CampusGear.Filters;
using CampusGear.Models;
using CampusGear.Services;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Controllers;

[Route("auth")]
[AutoValidateAntiforgeryToken]
[TypeFilter(typeof(AccountAntiforgeryRecoveryFilter))]
public sealed class AccountController : Controller
{
    private const string PendingVerificationUserId = "CampusGear.PendingVerificationUserId";
    private const string PendingAdministratorUserId = "CampusGear.PendingAdministratorUserId";
    private const string PendingAdministratorStamp = "CampusGear.PendingAdministratorStamp";
    private const string PendingResetUserId = "CampusGear.PendingResetUserId";
    private const string PendingResetStamp = "CampusGear.PendingResetStamp";
    private const string AuthorizedResetUserId = "CampusGear.AuthorizedResetUserId";
    private const string AuthorizedResetStamp = "CampusGear.AuthorizedResetStamp";
    private const string AuthorizedResetExpiresAt = "CampusGear.AuthorizedResetExpiresAt";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly EmailChallengeService _challenges;
    private readonly IWebHostEnvironment _environment;
    private readonly DevelopmentEmailSender? _developmentEmail;

    public AccountController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        EmailChallengeService challenges,
        IWebHostEnvironment environment,
        IServiceProvider services)
    {
        _db = db;
        _users = users;
        _signIn = signIn;
        _challenges = challenges;
        _environment = environment;
        _developmentEmail = services.GetService<DevelopmentEmailSender>();
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public async Task<IActionResult> Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var current = await _users.GetUserAsync(User);
            if (current is { IsActive: true }) return Redirect(await WorkspaceForAsync(current));
            await _signIn.SignOutAsync();
        }

        PopulateFeedback();
        return View();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-post")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginInput input)
    {
        if (!ModelState.IsValid) return LoginView(input.Email);

        ClearPendingAdministrator();
        HttpContext.Session.Remove(PendingVerificationUserId);

        var email = input.Email.Trim();
        var user = await _users.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            await AuditAsync("Auth.LoginPassword", "Rejected", user?.Id);
            ViewData["ErrorMessage"] = "The email or password is incorrect.";
            return LoginView(email);
        }

        if (!user.EmailConfirmed)
        {
            if (await _users.IsLockedOutAsync(user) || !await _users.CheckPasswordAsync(user, input.Password))
            {
                if (!await _users.IsLockedOutAsync(user)) await _users.AccessFailedAsync(user);
                await AuditAsync("Auth.LoginPassword", "Rejected", user.Id);
                ViewData["ErrorMessage"] = "The email or password is incorrect.";
                return LoginView(email);
            }

            await _users.ResetAccessFailedCountAsync(user);
            HttpContext.Session.SetString(PendingVerificationUserId, user.Id);
            var issued = await _challenges.IssueAsync(user, EmailChallengePurpose.SignupVerification,
                HttpContext.RequestAborted);
            if (issued.Status == ChallengeIssueStatus.DeliveryUnavailable)
            {
                await AuditAsync("Auth.LoginPassword", "CodeDeliveryFailed", user.Id);
                ViewData["ErrorMessage"] = "We could not send a verification email. Please try again.";
                return View("EmailVerification");
            }

            TempData["NoticeMessage"] = issued.Status == ChallengeIssueStatus.Cooldown
                ? "A code was requested recently. Please wait before requesting a new one."
                : "We sent a verification code to your email address.";
            await AuditAsync("Auth.LoginPassword", "EmailVerificationRequired", user.Id);
            return RedirectToAction(nameof(EmailVerification));
        }

        var passwordResult = await _signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
        if (!passwordResult.Succeeded)
        {
            await AuditAsync("Auth.LoginPassword", "Rejected", user.Id);
            ViewData["ErrorMessage"] = passwordResult.IsLockedOut
                ? "This account is temporarily locked. Try again later."
                : "The email or password is incorrect.";
            return LoginView(email);
        }

        if (await _users.IsInRoleAsync(user, "Administrator"))
        {
            // CheckPasswordSignInAsync validates the password without creating an auth cookie.
            HttpContext.Session.SetString(PendingAdministratorUserId, user.Id);
            HttpContext.Session.SetString(PendingAdministratorStamp, user.SecurityStamp ?? string.Empty);
            var issued = await _challenges.IssueAsync(user, EmailChallengePurpose.AdministratorLogin,
                HttpContext.RequestAborted);
            if (issued.Status == ChallengeIssueStatus.DeliveryUnavailable)
            {
                await AuditAsync("Auth.LoginPassword", "CodeDeliveryFailed", user.Id);
                ViewData["ErrorMessage"] = "We could not send the administrator code. Please try again.";
                return LoginView(email);
            }

            TempData["NoticeMessage"] = issued.Status == ChallengeIssueStatus.Cooldown
                ? "A code was requested recently. Please wait before requesting a new one."
                : "We sent an administrator sign-in code to your email.";
            await AuditAsync("Auth.LoginPassword", "SecondFactorRequired", user.Id);
            return RedirectToAction(nameof(TwoFactor));
        }

        await AuditAsync("Auth.Login", "Succeeded", user.Id);
        await _signIn.SignInAsync(user, isPersistent: false);
        return Redirect(await WorkspaceForAsync(user));
    }

    [AllowAnonymous]
    [HttpGet("signup")]
    public IActionResult SignUp()
    {
        PopulateFeedback();
        return View();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-post")]
    [HttpPost("signup")]
    public async Task<IActionResult> SignUp([FromForm] SignUpInput input)
    {
        if (!ModelState.IsValid) return SignUpView(input);

        var email = input.Email.Trim();
        var existing = await _users.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Resume a pending account only after validating its original password.
            // Retrying signup must never replace the account's password or profile.
            if (existing.IsActive && !existing.EmailConfirmed && !await _users.IsLockedOutAsync(existing))
            {
                if (await _users.CheckPasswordAsync(existing, input.Password))
                {
                    await _users.ResetAccessFailedCountAsync(existing);
                    HttpContext.Session.SetString(PendingVerificationUserId, existing.Id);
                    var retry = await _challenges.IssueAsync(existing, EmailChallengePurpose.SignupVerification,
                        HttpContext.RequestAborted);
                    SetIssueFeedback(retry);
                    await AuditAsync("Auth.SignUp", "ResumedPendingVerification", existing.Id);
                    return RedirectToAction(nameof(EmailVerification));
                }
                await _users.AccessFailedAsync(existing);
            }
            await AuditAsync("Auth.SignUp", "DuplicateEmail", null);
            ModelState.AddModelError(nameof(input.Email), "An account already uses this email address. Sign in to continue verification or reset your password.");
            return SignUpView(input);
        }

        ApplicationUser user;
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                FullName = input.FullName.Trim(),
                PhoneNumber = input.MobileNumber.Trim(),
                IsActive = true
            };
            var created = await _users.CreateAsync(user, input.Password);
            if (!created.Succeeded)
            {
                foreach (var error in created.Errors)
                    ModelState.AddModelError(nameof(input.Password), error.Description);
                return SignUpView(input);
            }

            var assigned = await _users.AddToRoleAsync(user, "Borrower");
            if (!assigned.Succeeded)
            {
                ViewData["ErrorMessage"] = "Account creation is unavailable. Please try again later.";
                return SignUpView(input);
            }

            _db.BorrowerProfiles.Add(new BorrowerProfile
            {
                UserId = user.Id,
                ContactNumber = input.MobileNumber.Trim(),
                IsEligible = true
            });
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            await transaction.CommitAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException
               { Number: 2601 or 2627 })
        {
            // The unique email index also protects a race between simultaneous signups.
            ModelState.AddModelError(nameof(input.Email), "An account already uses this email address.");
            return SignUpView(input);
        }

        await AuditAsync("Auth.SignUp", "CreatedPendingVerification", user.Id);
        HttpContext.Session.SetString(PendingVerificationUserId, user.Id);
        var issued = await _challenges.IssueAsync(user, EmailChallengePurpose.SignupVerification,
            HttpContext.RequestAborted);
        if (issued.Status == ChallengeIssueStatus.DeliveryUnavailable)
        {
            TempData["ErrorMessage"] = "Your account was created, but we could not send the code. Try resending it.";
        }
        else
        {
            TempData["NoticeMessage"] = "We sent a verification code to your email address.";
        }

        return RedirectToAction(nameof(EmailVerification));
    }

    [AllowAnonymous]
    [HttpGet("email-verification")]
    public IActionResult EmailVerification()
    {
        if (HttpContext.Session.GetString(PendingVerificationUserId) is null)
            return RedirectToAction(nameof(Login));
        PopulateFeedback();
        return View();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-post")]
    [HttpPost("email-verification")]
    public async Task<IActionResult> EmailVerification([FromForm] CodeInput input)
    {
        var userId = HttpContext.Session.GetString(PendingVerificationUserId);
        if (userId is null) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid) return View();

        var result = await _challenges.VerifyAsync(userId, EmailChallengePurpose.SignupVerification,
            input.Code, HttpContext.RequestAborted);
        if (result != ChallengeVerifyStatus.Verified)
        {
            await AuditAsync("Auth.EmailVerification", result.ToString(), userId);
            ViewData["ErrorMessage"] = ChallengeError(result);
            return View();
        }

        var user = await _users.FindByIdAsync(userId);
        if (user is null || !user.IsActive) return RedirectToAction(nameof(Login));
        user.EmailConfirmed = true;
        user.UpdatedAtUtc = DateTime.UtcNow;
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            await AuditAsync("Auth.EmailVerification", "UpdateFailed", user.Id);
            ViewData["ErrorMessage"] = "We could not finish verification. Please contact support.";
            return View();
        }

        await AuditAsync("Auth.EmailVerification", "Succeeded", user.Id);
        HttpContext.Session.Remove(PendingVerificationUserId);
        if (await _users.IsInRoleAsync(user, "Administrator") ||
            await _users.IsInRoleAsync(user, "Custodian"))
        {
            TempData["NoticeMessage"] = "Email verified. Sign in to continue.";
            return RedirectToAction(nameof(Login));
        }
        await _signIn.SignInAsync(user, isPersistent: false);
        return Redirect("/auth/success?kind=signup");
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-post")]
    [HttpPost("resend-email-verification")]
    public async Task<IActionResult> ResendEmailVerification()
    {
        var userId = HttpContext.Session.GetString(PendingVerificationUserId);
        if (userId is null) return RedirectToAction(nameof(Login));
        var user = await _users.FindByIdAsync(userId);
        if (user is null || user.EmailConfirmed || !user.IsActive) return RedirectToAction(nameof(Login));

        var result = await _challenges.IssueAsync(user, EmailChallengePurpose.SignupVerification,
            HttpContext.RequestAborted);
        SetIssueFeedback(result);
        return RedirectToAction(nameof(EmailVerification));
    }

    [AllowAnonymous]
    [HttpGet("two-factor")]
    public async Task<IActionResult> TwoFactor()
    {
        if (await PendingAdministratorAsync() is null)
            return RedirectToAction(nameof(Login));
        PopulateFeedback();
        return View();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-post")]
    [HttpPost("two-factor")]
    public async Task<IActionResult> TwoFactor([FromForm] CodeInput input)
    {
        var user = await PendingAdministratorAsync();
        if (user is null) return RedirectToAction(nameof(Login));
        var userId = user.Id;
        if (!ModelState.IsValid) return View();

        var result = await _challenges.VerifyAsync(userId, EmailChallengePurpose.AdministratorLogin,
            input.Code, HttpContext.RequestAborted);
        if (result != ChallengeVerifyStatus.Verified)
        {
            await AuditAsync("Auth.AdministratorCode", result.ToString(), userId);
            ViewData["ErrorMessage"] = ChallengeError(result);
            return View();
        }

        await _db.Entry(user).ReloadAsync(HttpContext.RequestAborted);
        if (!await MatchesPendingAdministratorAsync(user))
        {
            ClearPendingAdministrator();
            await AuditAsync("Auth.AdministratorCode", "AccountUnavailable", userId);
            return RedirectToAction(nameof(Login));
        }

        await AuditAsync("Auth.AdministratorCode", "Succeeded", user.Id);
        ClearPendingAdministrator();
        await _signIn.SignInAsync(user, isPersistent: false);
        return Redirect("/admin/dashboard");
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-post")]
    [HttpPost("resend-two-factor")]
    public async Task<IActionResult> ResendTwoFactor()
    {
        var user = await PendingAdministratorAsync();
        if (user is null) return RedirectToAction(nameof(Login));

        var result = await _challenges.IssueAsync(user, EmailChallengePurpose.AdministratorLogin,
            HttpContext.RequestAborted);
        SetIssueFeedback(result);
        return RedirectToAction(nameof(TwoFactor));
    }

    [AllowAnonymous]
    [HttpGet("password-reset-request")]
    public IActionResult PasswordResetRequest()
    {
        PopulateFeedback();
        return View();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-post")]
    [HttpPost("password-reset-request")]
    public async Task<IActionResult> PasswordResetRequest([FromForm] EmailInput input)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Email"] = input.Email;
            return View();
        }

        var user = await _users.FindByEmailAsync(input.Email.Trim());
        HttpContext.Session.SetString(PendingResetUserId, user is { IsActive: true } ? user.Id : string.Empty);
        HttpContext.Session.SetString(PendingResetStamp, user is { IsActive: true } ? user.SecurityStamp ?? string.Empty : string.Empty);
        ClearAuthorizedReset();
        if (user is { IsActive: true })
        {
            var issued = await _challenges.IssueAsync(user, EmailChallengePurpose.PasswordReset,
                HttpContext.RequestAborted);
            await AuditAsync("Auth.PasswordResetRequest", issued.Status.ToString(), user.Id);
        }

        // The response is intentionally identical for known and unknown addresses.
        TempData["NoticeMessage"] = "If an account uses that email, a reset code has been sent.";
        return RedirectToAction(nameof(PasswordResetCode));
    }

    [AllowAnonymous]
    [HttpGet("password-reset-code")]
    public IActionResult PasswordResetCode()
    {
        if (HttpContext.Session.GetString(PendingResetUserId) is null)
            return RedirectToAction(nameof(PasswordResetRequest));
        PopulateFeedback();
        return View();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-post")]
    [HttpPost("password-reset-code")]
    public async Task<IActionResult> PasswordResetCode([FromForm] CodeInput input)
    {
        var userId = HttpContext.Session.GetString(PendingResetUserId);
        if (userId is null) return RedirectToAction(nameof(PasswordResetRequest));
        if (!ModelState.IsValid) return View();
        if (userId.Length == 0)
        {
            await AuditAsync("Auth.PasswordResetCode", "Rejected", null);
            ViewData["ErrorMessage"] = "The code is invalid or has expired.";
            return View();
        }

        var user = await _users.FindByIdAsync(userId);
        if (!MatchesPendingReset(user))
        {
            ClearPendingReset();
            TempData["ErrorMessage"] = "This reset request is no longer valid. Request a new code.";
            await AuditAsync("Auth.PasswordResetCode", "StaleFlow", userId);
            return RedirectToAction(nameof(PasswordResetRequest));
        }

        var result = await _challenges.VerifyAsync(userId, EmailChallengePurpose.PasswordReset,
            input.Code, HttpContext.RequestAborted);
        if (result != ChallengeVerifyStatus.Verified)
        {
            await AuditAsync("Auth.PasswordResetCode", result.ToString(), userId);
            ViewData["ErrorMessage"] = ChallengeError(result);
            return View();
        }

        await _db.Entry(user!).ReloadAsync(HttpContext.RequestAborted);
        if (_db.Entry(user!).State == EntityState.Detached || !MatchesPendingReset(user))
        {
            ClearPendingReset();
            TempData["ErrorMessage"] = "This reset request is no longer valid. Request a new code.";
            return RedirectToAction(nameof(PasswordResetRequest));
        }

        await AuditAsync("Auth.PasswordResetCode", "Succeeded", userId);
        ClearPendingReset();
        HttpContext.Session.SetString(AuthorizedResetUserId, userId);
        HttpContext.Session.SetString(AuthorizedResetStamp, user!.SecurityStamp ?? string.Empty);
        HttpContext.Session.SetString(AuthorizedResetExpiresAt,
            DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        return RedirectToAction(nameof(PasswordResetNew));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-post")]
    [HttpPost("resend-password-reset-code")]
    public async Task<IActionResult> ResendPasswordResetCode()
    {
        var userId = HttpContext.Session.GetString(PendingResetUserId);
        if (userId is null) return RedirectToAction(nameof(PasswordResetRequest));
        if (userId.Length > 0)
        {
            var user = await _users.FindByIdAsync(userId);
            if (!MatchesPendingReset(user))
            {
                ClearPendingReset();
                return RedirectToAction(nameof(PasswordResetRequest));
            }
            await _challenges.IssueAsync(user!, EmailChallengePurpose.PasswordReset,
                HttpContext.RequestAborted);
        }
        TempData["NoticeMessage"] = "If an account uses that email, a reset code has been sent.";
        return RedirectToAction(nameof(PasswordResetCode));
    }

    [AllowAnonymous]
    [HttpGet("password-reset-new")]
    public async Task<IActionResult> PasswordResetNew()
    {
        if (await AuthorizedResetUserAsync() is null)
            return RedirectToAction(nameof(PasswordResetRequest));
        PopulateFeedback();
        return View();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-post")]
    [HttpPost("password-reset-new")]
    public async Task<IActionResult> PasswordResetNew([FromForm] NewPasswordInput input)
    {
        var user = await AuthorizedResetUserAsync();
        if (user is null) return RedirectToAction(nameof(PasswordResetRequest));
        if (!ModelState.IsValid) return View();

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, input.Password);
        if (!result.Succeeded)
        {
            await AuditAsync("Auth.PasswordReset", "Rejected", user.Id);
            foreach (var error in result.Errors)
                ModelState.AddModelError(nameof(input.Password), error.Description);
            return View();
        }

        await AuditAsync("Auth.PasswordReset", "Succeeded", user.Id);
        ClearAuthorizedReset();
        await _signIn.SignOutAsync();
        return Redirect("/auth/success?kind=password-reset");
    }

    [AllowAnonymous]
    [HttpGet("success")]
    public IActionResult Success([FromQuery] string? kind)
    {
        ViewData["SuccessKind"] = kind == "password-reset" ? "password-reset" : "signup";
        return View();
    }

    [AllowAnonymous]
    [HttpGet("access-denied")]
    public IActionResult AccessDenied() => View();

    [Authorize]
    [EnableRateLimiting("auth-post")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await AuditAsync("Auth.Logout", "Succeeded", _users.GetUserId(User));
        await _signIn.SignOutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("development-inbox")]
    public IActionResult DevelopmentInbox()
    {
        if (!_environment.IsDevelopment() ||
            HttpContext.Connection.RemoteIpAddress is not IPAddress remoteAddress ||
            !IPAddress.IsLoopback(remoteAddress) ||
            _developmentEmail is null)
            return NotFound();

        Response.Headers.CacheControl = "no-store";
        return Json(_developmentEmail.GetRecent());
    }

    private ViewResult LoginView(string? email)
    {
        ViewData["Email"] = email;
        return View("Login");
    }

    private ViewResult SignUpView(SignUpInput input)
    {
        ViewData["FullName"] = input.FullName;
        ViewData["Email"] = input.Email;
        ViewData["MobileNumber"] = input.MobileNumber;
        return View("SignUp");
    }

    private async Task<string> WorkspaceForAsync(ApplicationUser user)
    {
        if (await _users.IsInRoleAsync(user, "Administrator")) return "/admin/dashboard";
        if (await _users.IsInRoleAsync(user, "Custodian")) return "/custodian/dashboard";
        if (await _users.IsInRoleAsync(user, "Borrower")) return "/borrower/dashboard";
        return "/auth/access-denied";
    }

    private void PopulateFeedback()
    {
        if (TempData["ErrorMessage"] is string error) ViewData["ErrorMessage"] = error;
        if (TempData["NoticeMessage"] is string notice) ViewData["NoticeMessage"] = notice;
    }

    private void SetIssueFeedback(ChallengeIssueResult result)
    {
        switch (result.Status)
        {
            case ChallengeIssueStatus.Sent:
                TempData["NoticeMessage"] = "A new code was sent to your email address.";
                break;
            case ChallengeIssueStatus.Cooldown:
                TempData["NoticeMessage"] = "Please wait before requesting another code.";
                break;
            default:
                TempData["ErrorMessage"] = "We could not send a code. Please try again.";
                break;
        }
    }

    private static string ChallengeError(ChallengeVerifyStatus status) => status switch
    {
        ChallengeVerifyStatus.Expired => "The code expired. Request a new one.",
        ChallengeVerifyStatus.TooManyAttempts => "Too many incorrect attempts. Request a new code.",
        _ => "The code is invalid or has already been used."
    };

    private async Task<ApplicationUser?> PendingAdministratorAsync()
    {
        var id = HttpContext.Session.GetString(PendingAdministratorUserId);
        var user = id is null ? null : await _users.FindByIdAsync(id);
        if (user is not null && await MatchesPendingAdministratorAsync(user)) return user;
        ClearPendingAdministrator();
        if (id is not null) TempData["ErrorMessage"] = "This sign-in request is no longer valid. Sign in again.";
        return null;
    }

    private async Task<bool> MatchesPendingAdministratorAsync(ApplicationUser user)
    {
        var stamp = HttpContext.Session.GetString(PendingAdministratorStamp);
        return user.IsActive && user.EmailConfirmed && !string.IsNullOrEmpty(stamp) &&
               user.Id == HttpContext.Session.GetString(PendingAdministratorUserId) &&
               string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal) &&
               await _users.IsInRoleAsync(user, "Administrator");
    }

    private bool MatchesPendingReset(ApplicationUser? user)
    {
        var stamp = HttpContext.Session.GetString(PendingResetStamp);
        return user is { IsActive: true } && !string.IsNullOrEmpty(stamp) &&
               user.Id == HttpContext.Session.GetString(PendingResetUserId) &&
               string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal);
    }

    private async Task<ApplicationUser?> AuthorizedResetUserAsync()
    {
        var id = HttpContext.Session.GetString(AuthorizedResetUserId);
        var stamp = HttpContext.Session.GetString(AuthorizedResetStamp);
        var expiry = HttpContext.Session.GetString(AuthorizedResetExpiresAt);
        if (id is null || string.IsNullOrEmpty(stamp) ||
            !long.TryParse(expiry, NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAt) ||
            expiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            ClearAuthorizedReset();
            if (id is not null) TempData["ErrorMessage"] = "Your password reset permission expired. Request a new code.";
            return null;
        }

        var user = await _users.FindByIdAsync(id);
        if (user is { IsActive: true } && string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal))
            return user;
        ClearAuthorizedReset();
        TempData["ErrorMessage"] = "This reset request is no longer valid. Request a new code.";
        return null;
    }

    private void ClearPendingAdministrator()
    {
        HttpContext.Session.Remove(PendingAdministratorUserId);
        HttpContext.Session.Remove(PendingAdministratorStamp);
    }

    private void ClearPendingReset()
    {
        HttpContext.Session.Remove(PendingResetUserId);
        HttpContext.Session.Remove(PendingResetStamp);
    }

    private void ClearAuthorizedReset()
    {
        HttpContext.Session.Remove(AuthorizedResetUserId);
        HttpContext.Session.Remove(AuthorizedResetStamp);
        HttpContext.Session.Remove(AuthorizedResetExpiresAt);
    }

    private async Task AuditAsync(string action, string outcome, string? userId)
    {
        _db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = User.Identity?.IsAuthenticated == true ? _users.GetUserId(User) : null,
            Action = action,
            EntityType = "ApplicationUser",
            EntityId = userId ?? "unknown",
            Outcome = outcome,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = HttpContext.TraceIdentifier
        });
        await _db.SaveChangesAsync(HttpContext.RequestAborted);
    }
}

public sealed class LoginInput
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class SignUpInput
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, Phone, StringLength(40)]
    public string MobileNumber { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class CodeInput
{
    [Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "Enter the six-digit code.")]
    public string Code { get; set; } = string.Empty;
}

public sealed class EmailInput
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public sealed class NewPasswordInput
{
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
