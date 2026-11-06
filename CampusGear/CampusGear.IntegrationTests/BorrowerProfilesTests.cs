using System.Globalization;
using System.Net;
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>
/// WBS 19.0 Borrower Profile List and 20.0 Profile Details &amp; Eligibility: the custodian and administrator borrower
/// lists filter correctly, and a borrower whose access is paused cannot make a reservation.
/// Runs the real web app against a throwaway SQL Server database (see <see cref="WebAppFixture"/>).
/// </summary>
[Collection("Web app permissions")]
public sealed class BorrowerProfilesTests(WebAppFixture app)
{
    private const string PausedMessage = "Your borrowing access is currently paused. Contact an administrator.";

    /// <summary>Searching by the account's unique email keeps each check independent of other accounts and paging.</summary>
    private async Task<bool> ListedAsync(string role, string listPath, string filter, string email)
    {
        var html = await app.Client(role).GetStringAsync($"{listPath}?{filter}&q={Uri.EscapeDataString(email)}");
        // The search box echoes the query back in its value="..." attribute, so ignore attributes of that kind:
        // the email must appear as a row's visible text to count as listed.
        html = System.Text.RegularExpressions.Regex.Replace(html, "value=\"[^\"]*\"", string.Empty);
        return html.Contains(email, StringComparison.OrdinalIgnoreCase);
    }

    private Task SetEligibilityAsync(Guid profileId, bool eligible) => app.DbAsync(async db =>
    {
        var profile = await db.BorrowerProfiles.SingleAsync(x => x.Id == profileId);
        profile.IsEligible = eligible;
        await db.SaveChangesAsync();
        return 0;
    });

    private Task SetApprovalAsync(string userId, AccountApprovalStatus status) => app.DbAsync(async db =>
    {
        var user = await db.Users.SingleAsync(x => x.Id == userId);
        user.ApprovalStatus = status;
        await db.SaveChangesAsync();
        return 0;
    });

    // ---- 19.0 Borrower Profile List: custodian list (read-only), eligibility filter ----

    [Fact]
    public async Task Custodian_borrower_list_filters_by_eligibility()
    {
        var eligible = await app.NewAccountAsync("Borrower");
        var paused = await app.NewAccountAsync("Borrower");
        await SetEligibilityAsync(paused.ProfileId!.Value, false);
        const string list = "/custodian/borrowers";

        // Paused shows only paused borrowers.
        Assert.True(await ListedAsync("Custodian", list, "eligibility=paused", paused.Email));
        Assert.False(await ListedAsync("Custodian", list, "eligibility=paused", eligible.Email));

        // Eligible shows only eligible borrowers.
        Assert.True(await ListedAsync("Custodian", list, "eligibility=eligible", eligible.Email));
        Assert.False(await ListedAsync("Custodian", list, "eligibility=eligible", paused.Email));

        // All, and any unknown value, show everyone.
        foreach (var filter in new[] { "eligibility=all", "eligibility=nonsense", "" })
        {
            Assert.True(await ListedAsync("Custodian", list, filter, eligible.Email));
            Assert.True(await ListedAsync("Custodian", list, filter, paused.Email));
        }
    }

    // ---- 19.0 Borrower Profile List: administrator list, status filter ----

    [Fact]
    public async Task Administrator_borrower_list_filters_by_account_status()
    {
        var active = await app.NewAccountAsync("Borrower");
        var inactive = await app.NewAccountAsync("Borrower", active: false);
        var pending = await app.NewAccountAsync("Borrower");
        var rejected = await app.NewAccountAsync("Borrower");
        await SetApprovalAsync(pending.UserId, AccountApprovalStatus.PendingApproval);
        await SetApprovalAsync(rejected.UserId, AccountApprovalStatus.Rejected);
        const string list = "/admin/borrowers";

        // Each filter shows exactly the accounts with that status.
        var expected = new (string Filter, TestAccount[] Shown)[]
        {
            ("status=active", [active]),
            ("status=inactive", [inactive]),
            ("status=pending", [pending]),
            ("status=rejected", [rejected]),
            ("status=all", [active, inactive, pending, rejected]),
            ("status=nonsense", [active, inactive, pending, rejected])
        };
        var everyone = new[] { active, inactive, pending, rejected };
        foreach (var (filter, shown) in expected)
            foreach (var account in everyone)
                Assert.Equal(shown.Contains(account),
                    await ListedAsync("Administrator", list, filter, account.Email));
    }

    [Fact]
    public async Task Administrator_borrower_list_never_shows_staff_accounts()
    {
        var custodian = await app.NewAccountAsync("Custodian");
        var borrower = await app.NewAccountAsync("Borrower");

        Assert.True(await ListedAsync("Administrator", "/admin/borrowers", "status=all", borrower.Email));
        Assert.False(await ListedAsync("Administrator", "/admin/borrowers", "status=all", custodian.Email));
        // The custodian's own list is borrowers only as well.
        Assert.False(await ListedAsync("Custodian", "/custodian/borrowers", "eligibility=all", custodian.Email));
    }

    // ---- 20.0 Profile Details & Eligibility: a paused borrower cannot reserve ----

    [Fact]
    public async Task Paused_borrower_is_told_so_and_cannot_reserve_until_access_is_restored()
    {
        var borrower = await app.NewAccountAsync("Borrower");
        var (client, _) = await app.SignInAsync(borrower.Email);
        var itemId = await app.NewItemAsync();
        await SetEligibilityAsync(borrower.ProfileId!.Value, false);

        // The catalog still opens (browsing is allowed) and shows the paused panel.
        var page = await client.GetStringAsync("/borrower/reservation");
        Assert.Contains("Borrowing access is paused", page);

        // Submitting a request is refused with the paused message, and nothing is saved.
        var refused = await SubmitAsync(client, itemId, "Paused borrower request");
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains(PausedMessage, await refused.Content.ReadAsStringAsync());
        Assert.False(await app.DbAsync(db => db.Reservations.AnyAsync(x => x.BorrowerProfileId == borrower.ProfileId)));

        // The same request goes through once an administrator restores access.
        await SetEligibilityAsync(borrower.ProfileId!.Value, true);
        var accepted = await SubmitAsync(client, itemId, "Restored borrower request");
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.True(await app.DbAsync(db => db.Reservations.AnyAsync(x => x.BorrowerProfileId == borrower.ProfileId)));
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid itemId, string purpose)
    {
        var manila = ResolveManila();
        var start = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, manila).Date.AddDays(1).AddHours(9);
        return WebAppFixture.PostFormAsync(client, "/borrower/reservation", "/borrower/reservation",
            ("RequestId", Guid.NewGuid().ToString()), ("EquipmentItemId", itemId.ToString()), ("Quantity", "1"),
            ("StartLocal", start.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)),
            ("EndLocal", start.AddHours(2).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)),
            ("Purpose", purpose));
    }

    private static TimeZoneInfo ResolveManila()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
    }
}
