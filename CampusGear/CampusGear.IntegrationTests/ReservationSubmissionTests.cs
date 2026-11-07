using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>
/// WBS 23.0 Search &amp; Selection and 24.0 Request Submission: the Browse Equipment page and the reservation form,
/// run through the real pages as a signed-in borrower. Uses its own app instance and throwaway database.
/// </summary>
public sealed class ReservationSubmissionTests(WebAppFixture app) : IClassFixture<WebAppFixture>
{
    private static readonly TimeZoneInfo Manila = ResolveManila();

    // ---- WBS 23.0 Search & Selection ----

    [Fact]
    public async Task Catalog_shows_search_box_category_chips_available_filter_and_the_item()
    {
        var itemId = await app.NewItemAsync();
        var itemName = await app.DbAsync(db => db.EquipmentItems.Where(x => x.Id == itemId).Select(x => x.Name).SingleAsync());
        var category = await app.DbAsync(db => db.EquipmentCategories.Where(x => x.Id == app.CategoryId).Select(x => x.Name).SingleAsync());
        var encodedCategory = WebUtility.HtmlEncode(category);

        var html = await app.Client("Borrower").GetStringAsync("/borrower/reservation");

        Assert.Contains("data-catalog-search", html);                                   // search box
        Assert.Contains("data-category=\"\">All</button>", html);                       // "All" chip
        Assert.Contains($"data-category=\"{encodedCategory}\">{encodedCategory}</button>", html); // chip for the item's category
        Assert.Contains("data-catalog-available", html);                                // "Available to reserve" filter
        Assert.Contains($"data-name=\"{WebUtility.HtmlEncode(itemName)}\"", html);      // the item's card
    }

    [Theory]
    [InlineData("/borrower/reservation?q=permission")]
    [InlineData("/borrower/reservation?available=1")]
    [InlineData("/borrower/reservation?category=Anything")]
    public async Task Catalog_search_and_filter_links_open_the_page(string path)
    {
        // The filtering itself runs in the browser (borrower-catalog.js); this checks the links the home page
        // and other pages use still load the catalog for a borrower.
        var response = await app.Client("Borrower").GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-catalog-search", await response.Content.ReadAsStringAsync());
    }

    // ---- WBS 24.0 Request Submission ----

    [Fact]
    public async Task Valid_request_is_saved_as_pending()
    {
        var itemId = await app.NewItemAsync();
        var (start, end) = Tomorrow();

        var response = await SubmitAsync(itemId, start, end, "Class presentation");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("state=submitted", response.Headers.Location!.OriginalString);
        Assert.Equal(1, await ReservationCountAsync(itemId));
    }

    [Fact]
    public async Task Empty_purpose_is_refused()
    {
        var itemId = await app.NewItemAsync();
        var (start, end) = Tomorrow();

        var response = await SubmitAsync(itemId, start, end, purpose: "");

        await AssertRefusedAsync(response, itemId, "Enter the purpose of your request.");
    }

    [Fact]
    public async Task End_before_start_is_refused()
    {
        var itemId = await app.NewItemAsync();
        var (start, _) = Tomorrow();

        var response = await SubmitAsync(itemId, start, start.AddHours(-1), "Class presentation");

        await AssertRefusedAsync(response, itemId, "The end time must be after the start time.");
    }

    [Fact]
    public async Task Start_in_the_past_is_refused()
    {
        var itemId = await app.NewItemAsync();
        var (tomorrow, _) = Tomorrow();
        var yesterday = tomorrow.AddDays(-2);

        var response = await SubmitAsync(itemId, yesterday, yesterday.AddHours(2), "Class presentation");

        await AssertRefusedAsync(response, itemId, "Choose a future start time.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Quantity_out_of_range_is_refused(int quantity)
    {
        // NewItemAsync creates a single-unit item, so only a quantity of 1 is allowed.
        var itemId = await app.NewItemAsync();
        var (start, end) = Tomorrow();

        var response = await SubmitAsync(itemId, start, end, "Class presentation", quantity);

        await AssertRefusedAsync(response, itemId, "Only 1 unit of this equipment can be requested.");
    }

    // ---- Helpers ----

    /// <summary>The form is shown again (200, not a redirect) with the message, and nothing is saved.</summary>
    private async Task AssertRefusedAsync(HttpResponseMessage response, Guid itemId, string message)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, await ReservationCountAsync(itemId));
    }

    private Task<HttpResponseMessage> SubmitAsync(Guid itemId, DateTime startLocal, DateTime endLocal, string purpose,
        int quantity = 1) => WebAppFixture.PostFormAsync(app.Client("Borrower"), "/borrower/reservation", "/borrower/reservation",
        ("RequestId", Guid.NewGuid().ToString()), ("EquipmentItemId", itemId.ToString()),
        ("Quantity", quantity.ToString(CultureInfo.InvariantCulture)),
        ("StartLocal", startLocal.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)),
        ("EndLocal", endLocal.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)), ("Purpose", purpose));

    private Task<int> ReservationCountAsync(Guid itemId) =>
        app.DbAsync(db => db.Reservations.CountAsync(x => x.EquipmentItemId == itemId));

    /// <summary>Tomorrow 9:00–11:00 in Manila time, the form's local time zone.</summary>
    private static (DateTime Start, DateTime End) Tomorrow()
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Manila).Date.AddDays(1).AddHours(9);
        return (local, local.AddHours(2));
    }

    private static TimeZoneInfo ResolveManila()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
    }
}
