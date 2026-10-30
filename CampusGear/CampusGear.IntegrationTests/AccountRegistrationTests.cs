using System.Security.Claims;
using CampusGear.Controllers;
using CampusGear.Data;
using CampusGear.Models;
using CampusGear.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CampusGear.IntegrationTests;

[Collection("SQL reservation lifecycle")]
public sealed class AccountRegistrationTests(SqlReservationFixture fixture)
{
    private const string Password = "Integration-Pass!42";

    [Fact]
    public async Task Failed_delivery_is_logged_without_secrets_and_can_be_retried_immediately()
    {
        var borrower = await fixture.NewBorrowerAsync();
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == borrower.UserId);
        var sender = new TestSender { Fail = true };
        var logger = new TestLogger();
        var service = Challenges(db, sender, logger);

        Assert.Equal(ChallengeIssueStatus.DeliveryUnavailable,
            (await service.IssueAsync(user, EmailChallengePurpose.PasswordReset)).Status);
        var failed = await db.EmailChallenges.SingleAsync(x => x.UserId == user.Id);
        Assert.NotNull(failed.InvalidatedAtUtc);
        Assert.Contains(logger.Messages, x => x.Contains("SmtpException") && x.Contains("MailboxUnavailable"));
        Assert.DoesNotContain(logger.Messages, x => x.Contains("secret-marker") || x.Contains(user.Email!));

        sender.Fail = false;
        Assert.Equal(ChallengeIssueStatus.Sent,
            (await service.IssueAsync(user, EmailChallengePurpose.PasswordReset)).Status);
        Assert.Equal(ChallengeIssueStatus.Cooldown,
            (await service.IssueAsync(user, EmailChallengePurpose.PasswordReset)).Status);
        Assert.Equal(2, sender.Attempts);
        Assert.Equal(1, await db.EmailChallenges.CountAsync(x => x.UserId == user.Id && x.InvalidatedAtUtc == null));
    }

    [Fact]
    public async Task Signup_creates_pending_borrower_without_sending_a_code()
    {
        await using var scope = fixture.CreateScope();
        var sender = new TestSender();
        var (controller, db, _) = Account(scope, sender);
        var input = NewSignup();

        var result = await controller.SignUp(input);

        Assert.Equal(nameof(AccountController.RegistrationSubmitted), Assert.IsType<RedirectToActionResult>(result).ActionName);
        var user = await db.Users.Include(x => x.BorrowerProfile).SingleAsync(x => x.Email == input.Email);
        Assert.Equal(AccountApprovalStatus.PendingApproval, user.ApprovalStatus);
        Assert.False(user.EmailConfirmed);
        Assert.Equal("Maria", user.FirstName);
        Assert.Equal("Dela Cruz", user.LastName);
        Assert.Equal("Maria Dela Cruz", user.FullName);
        Assert.Equal(input.StudentIdNumber, user.BorrowerProfile!.StudentNumber);
        Assert.Equal(0, sender.Attempts);
        Assert.False(await db.EmailChallenges.AnyAsync(x => x.UserId == user.Id));
        Assert.Contains(await db.AuditEvents.Where(x => x.EntityId == user.Id).ToListAsync(),
            x => x.Action == "Auth.SignUp" && x.Outcome == "CreatedPendingApproval");
    }

    [Fact]
    public async Task Signup_rejects_registered_student_id_and_email()
    {
        await using var scope = fixture.CreateScope();
        var (controller, db, _) = Account(scope, new TestSender());
        var first = NewSignup();
        await controller.SignUp(first);

        var (sameStudent, _, _) = Account(scope, new TestSender());
        var duplicateStudent = NewSignup();
        duplicateStudent.StudentIdNumber = " " + first.StudentIdNumber.ToLowerInvariant() + " ";
        Assert.IsType<ViewResult>(await sameStudent.SignUp(duplicateStudent));
        Assert.Equal("This Student ID Number is already registered.",
            sameStudent.ModelState[nameof(SignUpInput.StudentIdNumber)]!.Errors.Single().ErrorMessage);

        var (sameEmail, _, _) = Account(scope, new TestSender());
        var duplicateEmail = NewSignup();
        duplicateEmail.Email = first.Email;
        Assert.IsType<ViewResult>(await sameEmail.SignUp(duplicateEmail));
        Assert.Equal("An account already uses this email address.",
            sameEmail.ModelState[nameof(SignUpInput.Email)]!.Errors.Single().ErrorMessage);

        Assert.False(await db.Users.AnyAsync(x => x.Email == duplicateStudent.Email));
        Assert.Equal(1, await db.Users.CountAsync(x => x.Email == first.Email));
    }

    [Fact]
    public async Task Login_explains_pending_and_rejected_registrations_after_password_check()
    {
        await using var scope = fixture.CreateScope();
        var (signup, db, users) = Account(scope, new TestSender());
        var input = NewSignup();
        await signup.SignUp(input);

        var (wrongPassword, _, _) = Account(scope, new TestSender());
        var view = Assert.IsType<ViewResult>(await wrongPassword.Login(new LoginInput { Email = input.Email, Password = "Wrong-Pass!42" }));
        Assert.Equal("The email or password is incorrect.", view.ViewData["ErrorMessage"]);

        var (pending, _, _) = Account(scope, new TestSender());
        view = Assert.IsType<ViewResult>(await pending.Login(new LoginInput { Email = input.Email, Password = Password }));
        Assert.Equal("Your account is pending administrator approval.", view.ViewData["ErrorMessage"]);

        var user = (await users.FindByEmailAsync(input.Email))!;
        user.ApprovalStatus = AccountApprovalStatus.Rejected;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        var (rejected, _, _) = Account(scope, new TestSender());
        view = Assert.IsType<ViewResult>(await rejected.Login(new LoginInput { Email = input.Email, Password = Password }));
        Assert.Equal("Your registration was not approved. Please contact the administrator.", view.ViewData["ErrorMessage"]);
        Assert.False(await db.EmailChallenges.AnyAsync(x => x.UserId == user.Id));
    }

    [Fact]
    public async Task Administrator_approval_and_rejection_update_status_and_audit_log()
    {
        await using var scope = fixture.CreateScope();
        var (signup, db, users) = Account(scope, new TestSender());
        var approveInput = NewSignup();
        var rejectInput = NewSignup();
        await signup.SignUp(approveInput);
        var (signupAgain, _, _) = Account(scope, new TestSender());
        await signupAgain.SignUp(rejectInput);
        var toApprove = (await users.FindByEmailAsync(approveInput.Email))!;
        var toReject = (await users.FindByEmailAsync(rejectInput.Email))!;

        Assert.IsType<RedirectResult>(await Admin(scope).Approve(toApprove.Id, "/admin/users?status=pending", default));
        Assert.IsType<RedirectResult>(await Admin(scope).Reject(toReject.Id, null, default));
        var refusal = Admin(scope);
        await refusal.Reject(toApprove.Id, null, default);
        Assert.NotNull(refusal.TempData["AdminUsersError"]);

        db.ChangeTracker.Clear();
        var approved = await db.Users.SingleAsync(x => x.Id == toApprove.Id);
        var rejected = await db.Users.SingleAsync(x => x.Id == toReject.Id);
        Assert.Equal(AccountApprovalStatus.Approved, approved.ApprovalStatus);
        Assert.True(approved.EmailConfirmed);
        Assert.Equal(AccountApprovalStatus.Rejected, rejected.ApprovalStatus);
        Assert.False(rejected.EmailConfirmed);
        Assert.True(await db.AuditEvents.AnyAsync(x => x.EntityId == approved.Id &&
            x.Action == "Admin.RegistrationApproved" && x.ActorUserId == fixture.AdminUserId));
        Assert.True(await db.AuditEvents.AnyAsync(x => x.EntityId == rejected.Id &&
            x.Action == "Admin.RegistrationRejected" && x.ActorUserId == fixture.AdminUserId));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.EntityId == approved.Id && x.Action == "Admin.RegistrationRejected"));
    }

    private static SignUpInput NewSignup()
    {
        var unique = Guid.NewGuid().ToString("N");
        return new SignUpInput
        {
            FirstName = " Maria ", LastName = "Dela Cruz", StudentIdNumber = "SID-" + unique[..12].ToUpperInvariant(),
            Email = $"{unique}@integration.test", MobileNumber = "09123456789",
            Password = Password, ConfirmPassword = Password
        };
    }

    private static (AccountController Controller, ApplicationDbContext Db, UserManager<ApplicationUser> Users) Account(
        AsyncServiceScope scope, TestSender sender)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = new DefaultHttpContext { Session = new TestSession(), RequestServices = scope.ServiceProvider };
        var controller = new AccountController(db, users, null!, Challenges(db, sender, new TestLogger()), null!, scope.ServiceProvider)
        {
            ControllerContext = new ControllerContext { HttpContext = context, RouteData = new(), ActionDescriptor = new() },
            TempData = new TempDataDictionary(context, new TestTempDataProvider())
        };
        controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(controller.ControllerContext);
        return (controller, db, users);
    }

    private AdminUsersController Admin(AsyncServiceScope scope)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, fixture.AdminUserId), new Claim(ClaimTypes.Role, "Administrator")], "Test"))
        };
        var controller = new AdminUsersController(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>())
        {
            ControllerContext = new ControllerContext { HttpContext = context, RouteData = new(), ActionDescriptor = new() },
            TempData = new TempDataDictionary(context, new TestTempDataProvider())
        };
        controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(controller.ControllerContext);
        return controller;
    }

    private static EmailChallengeService Challenges(ApplicationDbContext db, IEmailSender sender, TestLogger logger) =>
        new(db, sender, new EphemeralDataProtectionProvider(), logger);

    private sealed class TestSender : IEmailSender
    {
        public bool Fail { get; set; }
        public int Attempts { get; private set; }
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Fail) throw new System.Net.Mail.SmtpException(System.Net.Mail.SmtpStatusCode.MailboxUnavailable, "secret-marker");
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogger : ILogger<EmailChallengeService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];
        public bool IsAvailable => true;
        public string Id => "account-registration-test";
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
