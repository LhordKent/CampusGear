using System.Net;
using System.Text.RegularExpressions;
using CampusGear.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CampusGear.IntegrationTests;

/// <summary>
/// WBS 10.0 Password Reset Request: the request → code → new password flow, run through the real pages.
/// Uses its own app instance and throwaway database, so these sign-in posts never share the
/// 20-per-minute auth rate limit with the other web tests.
/// </summary>
public sealed partial class PasswordResetTests(WebAppFixture app) : IClassFixture<WebAppFixture>
{
    private const string SameMessage = "If an account uses that email, a reset code has been sent.";
    private const string WrongCodeMessage = "The code is invalid or has already been used.";
    private const string NewPassword = "Reset-Test!2026";

    [Fact]
    public async Task Unknown_email_gets_the_same_message_as_a_known_email()
    {
        var known = await app.NewAccountAsync("Borrower");
        var unknownEmail = $"nobody-{Guid.NewGuid():N}@permission.test";

        var (unknownResponse, unknownPage) = await RequestResetAsync(app.NewClient(), unknownEmail);
        var (knownResponse, knownPage) = await RequestResetAsync(app.NewClient(), known.Email);

        // Both go to the code page and show the same notice, so the page does not reveal which emails exist.
        Assert.Equal(HttpStatusCode.Redirect, unknownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, knownResponse.StatusCode);
        Assert.Equal("/auth/password-reset-code", unknownResponse.Headers.Location!.OriginalString);
        Assert.Equal("/auth/password-reset-code", knownResponse.Headers.Location!.OriginalString);
        Assert.Contains(SameMessage, unknownPage);
        Assert.Contains(SameMessage, knownPage);

        // Only the real account is sent a code.
        var mail = app.Services.GetRequiredService<DevelopmentEmailSender>().GetRecent();
        Assert.Contains(mail, x => x.ToEmail == known.Email);
        Assert.DoesNotContain(mail, x => x.ToEmail == unknownEmail);
    }

    [Fact]
    public async Task Wrong_code_is_refused()
    {
        var account = await app.NewAccountAsync("Borrower");
        var client = app.NewClient();
        await RequestResetAsync(client, account.Email);
        var wrongCode = WrongCodeFor(LatestCode(account.Email));

        var response = await WebAppFixture.PostFormAsync(client, "/auth/password-reset-code",
            "/auth/password-reset-code", ("Code", wrongCode));

        // The code page is shown again with an error, not the new-password page.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(WrongCodeMessage, await response.Content.ReadAsStringAsync());
        var newPasswordPage = await client.GetAsync("/auth/password-reset-new");
        Assert.Equal(HttpStatusCode.Redirect, newPasswordPage.StatusCode);
        Assert.Equal("/auth/password-reset-request", newPasswordPage.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Full_flow_sets_a_new_password_that_works_for_sign_in()
    {
        var account = await app.NewAccountAsync("Borrower");
        var client = app.NewClient();

        // 1. Request a reset.
        await RequestResetAsync(client, account.Email);

        // 2. Enter the emailed code.
        var codeResponse = await WebAppFixture.PostFormAsync(client, "/auth/password-reset-code",
            "/auth/password-reset-code", ("Code", LatestCode(account.Email)));
        Assert.Equal(HttpStatusCode.Redirect, codeResponse.StatusCode);
        Assert.Equal("/auth/password-reset-new", codeResponse.Headers.Location!.OriginalString);

        // 3. Choose a new password.
        var newPasswordResponse = await WebAppFixture.PostFormAsync(client, "/auth/password-reset-new",
            "/auth/password-reset-new", ("Password", NewPassword), ("ConfirmPassword", NewPassword));
        Assert.Equal(HttpStatusCode.Redirect, newPasswordResponse.StatusCode);
        Assert.Equal("/auth/success", newPasswordResponse.Headers.Location!.OriginalString);

        // 4. The new password signs in; the old one no longer does.
        var withNew = await SignInAsync(account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.Redirect, withNew.StatusCode);
        Assert.DoesNotContain("/auth/", withNew.Headers.Location!.OriginalString);

        var withOld = await SignInAsync(account.Email, WebAppFixture.Password);
        Assert.Equal(HttpStatusCode.OK, withOld.StatusCode);
        Assert.Contains("The email or password is incorrect.", await withOld.Content.ReadAsStringAsync());
    }

    /// <summary>Posts the reset request form and returns the response plus the code page it leads to.</summary>
    private static async Task<(HttpResponseMessage Response, string CodePage)> RequestResetAsync(HttpClient client, string email)
    {
        var response = await WebAppFixture.PostFormAsync(client, "/auth/password-reset-request",
            "/auth/password-reset-request", ("Email", email));
        var page = response.StatusCode == HttpStatusCode.Redirect
            ? await client.GetStringAsync(response.Headers.Location!.OriginalString)
            : string.Empty;
        return (response, page);
    }

    private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        WebAppFixture.PostFormAsync(app.NewClient(), "/auth/login", "/auth/login", ("Email", email), ("Password", password));

    /// <summary>The six-digit code in the newest email sent to this address by the development mailbox.</summary>
    private string LatestCode(string email)
    {
        var mail = app.Services.GetRequiredService<DevelopmentEmailSender>().GetRecent().First(x => x.ToEmail == email);
        return CodePattern().Match(mail.Body).Value;
    }

    /// <summary>A six-digit code guaranteed to differ from the real one (first digit changed).</summary>
    private static string WrongCodeFor(string code) => (code[0] == '9' ? '0' : (char)(code[0] + 1)) + code[1..];

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex CodePattern();
}
