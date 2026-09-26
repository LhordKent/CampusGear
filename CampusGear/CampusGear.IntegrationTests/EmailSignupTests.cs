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
public sealed class EmailSignupTests(SqlReservationFixture fixture)
{
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
            (await service.IssueAsync(user, EmailChallengePurpose.SignupVerification)).Status);
        var failed = await db.EmailChallenges.SingleAsync(x => x.UserId == user.Id);
        Assert.NotNull(failed.InvalidatedAtUtc);
        Assert.Contains(logger.Messages, x => x.Contains("SmtpException") && x.Contains("MailboxUnavailable"));
        Assert.DoesNotContain(logger.Messages, x => x.Contains("secret-marker") || x.Contains(user.Email!));

        sender.Fail = false;
        Assert.Equal(ChallengeIssueStatus.Sent,
            (await service.IssueAsync(user, EmailChallengePurpose.SignupVerification)).Status);
        Assert.Equal(ChallengeIssueStatus.Cooldown,
            (await service.IssueAsync(user, EmailChallengePurpose.SignupVerification)).Status);
        Assert.Equal(2, sender.Attempts);
        Assert.Equal(1, await db.EmailChallenges.CountAsync(x => x.UserId == user.Id && x.InvalidatedAtUtc == null));
    }

    [Theory]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, true, false, true, true)]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(false, true, true, false, false)]
    public async Task Signup_retry_requires_original_password_and_keeps_existing_account(
        bool confirmed, bool correctPassword, bool locked, bool deliveryFails, bool resumes)
    {
        var borrower = await fixture.NewBorrowerAsync();
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(borrower.UserId))!;
        user.EmailConfirmed = confirmed;
        user.LockoutEnabled = true;
        user.LockoutEnd = locked ? DateTimeOffset.UtcNow.AddMinutes(15) : null;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        var originalHash = user.PasswordHash;
        var originalName = user.FullName;
        var sender = new TestSender { Fail = deliveryFails };
        var context = new DefaultHttpContext { Session = new TestSession(), RequestServices = scope.ServiceProvider };
        var controller = new AccountController(db, users, null!, Challenges(db, sender, new TestLogger()), null!, scope.ServiceProvider)
        {
            ControllerContext = new ControllerContext { HttpContext = context, RouteData = new(), ActionDescriptor = new() },
            TempData = new TempDataDictionary(context, new TestTempDataProvider())
        };
        controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(controller.ControllerContext);

        var result = await controller.SignUp(new SignUpInput
        {
            Email = user.Email!, FullName = "Replacement must not be saved", MobileNumber = "09123456789",
            Password = correctPassword ? "Integration-Pass!42" : "Wrong-Pass!42",
            ConfirmPassword = correctPassword ? "Integration-Pass!42" : "Wrong-Pass!42"
        });

        if (resumes)
        {
            Assert.Equal("EmailVerification", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal(user.Id, context.Session.GetString("CampusGear.PendingVerificationUserId"));
            Assert.Equal(1, sender.Attempts);
            Assert.NotNull(controller.TempData[deliveryFails ? "ErrorMessage" : "NoticeMessage"]);
            Assert.Contains(await db.AuditEvents.Where(x => x.EntityId == user.Id).ToListAsync(),
                x => x.Outcome == "ResumedPendingVerification");
        }
        else
        {
            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Null(context.Session.GetString("CampusGear.PendingVerificationUserId"));
            Assert.Equal(0, sender.Attempts);
            if (!confirmed && !locked) Assert.Equal(1, user.AccessFailedCount);
        }
        Assert.Equal(originalHash, user.PasswordHash);
        Assert.Equal(originalName, user.FullName);
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == user.NormalizedEmail));
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
        public string Id => "email-signup-test";
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
