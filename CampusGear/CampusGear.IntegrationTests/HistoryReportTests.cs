using System.Text.RegularExpressions;
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>
/// WBS 36.0 History Report Filters and 37.0 History Listing &amp; Late Return Count: the borrowing history on
/// /custodian/history and /admin/history (the same report for both roles) filters by status and booking date, rejects
/// bad dates, and labels overdue and late-returned loans as late.
/// Runs the real web app against a throwaway SQL Server database (see <see cref="WebAppFixture"/>).
/// </summary>
[Collection("Web app permissions")]
public sealed class HistoryReportTests(WebAppFixture app)
{
    private static readonly (string Role, string Path)[] Reports =
        [("Custodian", "/custodian/history"), ("Administrator", "/admin/history")];

    private static readonly TimeZoneInfo Manila = ResolveManila();

    private static TimeZoneInfo ResolveManila()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
    }

    /// <summary>A Manila wall-clock time as the UTC time the database stores.</summary>
    private static DateTime ManilaToUtc(int year, int month, int day, int hour, int minute) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified), Manila);

    /// <summary>
    /// Creates a one-off equipment item and a reservation on it, plus a loan when a due time is given. Each record gets
    /// its own asset code, so a search for that code finds exactly this record whatever else is in the database.
    /// </summary>
    private async Task<string> SeedAsync(Guid profileId, ReservationStatus status, DateTime startUtc,
        DateTime? dueUtc = null, DateTime? returnedUtc = null)
    {
        var itemId = await app.NewItemAsync();
        var reservationId = await app.NewReservationAsync(profileId, itemId, status, startUtc, startUtc.AddHours(2));
        return await app.DbAsync(async db =>
        {
            if (dueUtc is { } due)
                db.Loans.Add(new Loan
                {
                    ReservationId = reservationId, ReleasedAtUtc = startUtc, DueAtUtc = due, ReturnedAtUtc = returnedUtc,
                    ReleasedByUserId = app.Custodian.UserId,
                    ConditionAtReturn = returnedUtc is null ? null : EquipmentCondition.Good
                });
            await db.SaveChangesAsync();
            return await db.EquipmentItems.Where(x => x.Id == itemId).Select(x => x.AssetCode).SingleAsync();
        });
    }

    /// <summary>The report's table row for this record, or null when the record is not listed.</summary>
    private async Task<string?> RowAsync(string role, string path, string filter, string assetCode)
    {
        var html = await app.Client(role).GetStringAsync($"{path}?{filter}&q={Uri.EscapeDataString(assetCode)}");
        // The search box echoes the query in a value="..." attribute; only a table row counts as "listed".
        html = Regex.Replace(html, "value=\"[^\"]*\"", string.Empty);
        var match = Regex.Match(html, $"<tr>(?:(?!</tr>).)*{Regex.Escape(assetCode)}(?:(?!</tr>).)*</tr>", RegexOptions.Singleline);
        return match.Success ? match.Value : null;
    }

    private async Task<TestAccount> NewBorrowerAsync() => await app.NewAccountAsync("Borrower");

    // ---- 36.0 History Report Filters: status ----

    [Fact]
    public async Task Status_filter_shows_only_the_matching_records_on_both_reports()
    {
        var profile = (await NewBorrowerAsync()).ProfileId!.Value;
        var now = DateTime.UtcNow;
        var future = now.AddDays(3);
        var records = new Dictionary<string, string>
        {
            ["pending"] = await SeedAsync(profile, ReservationStatus.Pending, future),
            ["approved"] = await SeedAsync(profile, ReservationStatus.Approved, future),
            ["rejected"] = await SeedAsync(profile, ReservationStatus.Rejected, future),
            ["cancelled"] = await SeedAsync(profile, ReservationStatus.Cancelled, future),
            ["expired"] = await SeedAsync(profile, ReservationStatus.Expired, future),
            // Out on loan and still within its due time.
            ["onloan"] = await SeedAsync(profile, ReservationStatus.Released, now.AddHours(-1), dueUtc: now.AddHours(2)),
            // Out on loan and past its due time.
            ["overdue"] = await SeedAsync(profile, ReservationStatus.Released, now.AddHours(-3), dueUtc: now.AddHours(-1)),
            // Returned before it was due.
            ["returned"] = await SeedAsync(profile, ReservationStatus.Completed, now.AddHours(-5),
                dueUtc: now.AddHours(-3), returnedUtc: now.AddHours(-4)),
            // Returned after it was due.
            ["returnedlate"] = await SeedAsync(profile, ReservationStatus.Completed, now.AddHours(-6),
                dueUtc: now.AddHours(-4), returnedUtc: now.AddHours(-2))
        };

        // Which records each filter must show. "Borrowed" includes the overdue loan (still out); "Returned"
        // includes the late return; "Late" is the overdue loan and the late return, and nothing else.
        var expected = new Dictionary<string, string[]>
        {
            ["status=pending"] = ["pending"],
            ["status=approved"] = ["approved"],
            ["status=borrowed"] = ["onloan", "overdue"],
            ["status=returned"] = ["returned", "returnedlate"],
            ["status=rejected"] = ["rejected"],
            ["status=cancelled"] = ["cancelled"],
            ["status=expired"] = ["expired"],
            ["status=late"] = ["overdue", "returnedlate"],
            ["status=all"] = [.. records.Keys],
            ["status=nonsense"] = [.. records.Keys]
        };

        foreach (var (role, path) in Reports)
            foreach (var (filter, shown) in expected)
                foreach (var (name, assetCode) in records)
                    Assert.True(shown.Contains(name) == (await RowAsync(role, path, filter, assetCode) is not null),
                        $"{role} {path}?{filter}: record '{name}' should {(shown.Contains(name) ? "" : "not ")}be listed");
    }

    // ---- 37.0 History Listing: Late labels ----

    [Fact]
    public async Task Overdue_and_late_returned_records_are_labelled_late_and_on_time_ones_are_not()
    {
        var profile = (await NewBorrowerAsync()).ProfileId!.Value;
        var now = DateTime.UtcNow;
        var onLoan = await SeedAsync(profile, ReservationStatus.Released, now.AddHours(-1), dueUtc: now.AddHours(2));
        var overdue = await SeedAsync(profile, ReservationStatus.Released, now.AddHours(-3), dueUtc: now.AddHours(-1));
        var returned = await SeedAsync(profile, ReservationStatus.Completed, now.AddHours(-5),
            dueUtc: now.AddHours(-3), returnedUtc: now.AddHours(-4));
        var returnedLate = await SeedAsync(profile, ReservationStatus.Completed, now.AddHours(-6),
            dueUtc: now.AddHours(-4), returnedUtc: now.AddHours(-2));

        foreach (var (role, path) in Reports)
        {
            Assert.Contains("Overdue", (await RowAsync(role, path, "status=all", overdue))!);
            Assert.Contains("Returned late", (await RowAsync(role, path, "status=all", returnedLate))!);

            var onTime = (await RowAsync(role, path, "status=all", returned))!;
            Assert.Contains("Returned", onTime);
            Assert.DoesNotContain("Returned late", onTime);
            Assert.DoesNotContain("Overdue", onTime);

            var stillOut = (await RowAsync(role, path, "status=all", onLoan))!;
            Assert.Contains("Borrowed", stillOut);
            Assert.DoesNotContain("Overdue", stillOut);
        }
    }

    // ---- 36.0 History Report Filters: booking dates ----

    [Fact]
    public async Task Date_filter_uses_the_booking_start_date_in_Manila_time_and_includes_both_end_days()
    {
        var profile = (await NewBorrowerAsync()).ProfileId!.Value;
        var early = await SeedAsync(profile, ReservationStatus.Approved, ManilaToUtc(2026, 2, 10, 9, 0));
        var lateNight = await SeedAsync(profile, ReservationStatus.Approved, ManilaToUtc(2026, 2, 28, 23, 30));
        var nextDay = await SeedAsync(profile, ReservationStatus.Approved, ManilaToUtc(2026, 3, 1, 0, 30));
        var all = new[] { early, lateNight, nextDay };

        // Each filter and the records it must show. 11:30 PM on Feb 28 is still Feb 28 in Manila,
        // and 12:30 AM on Mar 1 is already Mar 1, even though both are close in UTC.
        var expected = new (string Filter, string[] Shown)[]
        {
            ("from=2026-02-10&to=2026-02-10", [early]),
            ("from=2026-02-10&to=2026-02-28", [early, lateNight]),
            ("from=2026-02-11", [lateNight, nextDay]),
            ("to=2026-02-27", [early]),
            ("from=2026-03-01", [nextDay]),
            ("from=2026-02-11&to=2026-02-27", [])
        };

        foreach (var (role, path) in Reports)
            foreach (var (filter, shown) in expected)
                foreach (var assetCode in all)
                    Assert.True(shown.Contains(assetCode) == (await RowAsync(role, path, filter, assetCode) is not null),
                        $"{role} {path}?{filter}: record {assetCode} should {(shown.Contains(assetCode) ? "" : "not ")}be listed");
    }

    [Theory]
    [InlineData("from=not-a-date", "Use valid dates for the date filters.")]
    [InlineData("to=2026-13-45", "Use valid dates for the date filters.")]
    [InlineData("from=1999-12-31", "Choose filter dates between 2000 and 2100.")]
    [InlineData("to=2101-01-01", "Choose filter dates between 2000 and 2100.")]
    [InlineData("from=2026-03-01&to=2026-02-01", "The end date must be on or after the start date.")]
    public async Task Bad_date_filters_show_an_error_message_on_both_reports(string filter, string message)
    {
        foreach (var (role, path) in Reports)
            Assert.Contains(message, await app.Client(role).GetStringAsync($"{path}?{filter}"));
    }

    [Fact]
    public async Task A_valid_date_filter_shows_no_error_message()
    {
        foreach (var (role, path) in Reports)
        {
            var html = await app.Client(role).GetStringAsync($"{path}?from=2026-02-01&to=2026-02-28");
            Assert.DoesNotContain("Use valid dates for the date filters.", html);
            Assert.DoesNotContain("Choose filter dates between 2000 and 2100.", html);
            Assert.DoesNotContain("The end date must be on or after the start date.", html);
        }
    }
}
