using System.Net;
using System.Text.Json;
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>
/// Maintenance dialogs (JSON saves), automatic-search query handling, and Demo Mode isolation, over real HTTP and SQL.
/// </summary>
[Collection("Web app permissions")]
public sealed class Round3BehaviourTests(WebAppFixture app)
{
    private static async Task<HttpResponseMessage> PostFetchAsync(HttpClient client, string path, string tokenPage,
        params (string Name, string Value)[] fields)
    {
        var page = await client.GetStringAsync(tokenPage);
        var token = System.Text.RegularExpressions.Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(fields.Select(x => new KeyValuePair<string, string>(x.Name, x.Value))
                .Append(new("__RequestVerificationToken", token)))
        };
        request.Headers.Add("X-Requested-With", "fetch");
        request.Headers.Accept.ParseAdd("application/json");
        return await client.SendAsync(request);
    }

    private static async Task<(bool Ok, string Message, string? Field)> ReadAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        return (root.GetProperty("ok").GetBoolean(), root.GetProperty("message").GetString()!,
            root.GetProperty("field").ValueKind == JsonValueKind.Null ? null : root.GetProperty("field").GetString());
    }

    [Fact]
    public async Task Maintenance_dialog_saves_return_json_and_keep_errors_in_the_dialog()
    {
        var custodian = app.Client("Custodian");
        var itemId = await app.NewItemAsync();

        // A missing description is refused with 422, naming the field, and nothing is saved.
        var refused = await PostFetchAsync(custodian, "/admin/maintenance/open", "/admin/maintenance", ("equipmentItemId", itemId.ToString()), ("description", " "));
        Assert.Equal((HttpStatusCode)422, refused.StatusCode);
        var error = await ReadAsync(refused);
        Assert.False(error.Ok);
        Assert.Equal("description", error.Field);
        Assert.False(await app.DbAsync(db => db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == itemId)));

        // A valid case opens, holds the item, and appears in the list.
        var opened = await ReadAsync(await PostFetchAsync(custodian, "/admin/maintenance/open", "/admin/maintenance",
            ("equipmentItemId", itemId.ToString()), ("description", "Strap buckle broken.")));
        Assert.True(opened.Ok, opened.Message);
        var caseRow = await app.DbAsync(db => db.MaintenanceCases.Include(x => x.EquipmentItem).SingleAsync(x => x.EquipmentItemId == itemId));
        Assert.True(caseRow.EquipmentItem.IsMaintenanceHold);
        Assert.Contains(caseRow.EquipmentItem.AssetCode, await custodian.GetStringAsync("/admin/maintenance?q=" + caseRow.EquipmentItem.AssetCode));

        // Closing without repair notes is refused; with notes it closes and releases the hold.
        var version = Convert.ToBase64String(caseRow.RowVersion);
        var noNotes = await ReadAsync(await PostFetchAsync(custodian, $"/admin/maintenance/{caseRow.Id}/update", "/admin/maintenance",
            ("RowVersion", version), ("Status", "Closed"), ("Condition", "Good"), ("Resolution", "")));
        Assert.False(noNotes.Ok);
        Assert.Equal("Resolution", noNotes.Field);
        var closed = await ReadAsync(await PostFetchAsync(custodian, $"/admin/maintenance/{caseRow.Id}/update", "/admin/maintenance",
            ("RowVersion", version), ("Status", "Closed"), ("Condition", "Good"), ("Resolution", "Replaced the buckle.")));
        Assert.True(closed.Ok, closed.Message);
        Assert.False(await app.DbAsync(db => db.EquipmentItems.Where(x => x.Id == itemId).Select(x => x.IsMaintenanceHold).SingleAsync()));

        // The same save without JavaScript still redirects back to the list.
        var plain = await WebAppFixture.PostFormAsync(custodian, "/admin/maintenance/open", "/admin/maintenance", ("equipmentItemId", Guid.Empty.ToString()), ("description", "x"));
        Assert.Equal(HttpStatusCode.Redirect, plain.StatusCode);
    }

    [Theory]
    [InlineData("Administrator", "/admin/categories?q=zz-none", "No matching categories")]
    [InlineData("Administrator", "/admin/maintenance?q=zz-none", "No matching cases")]
    [InlineData("Administrator", "/admin/users?q=zz-none", "No matching accounts")]
    [InlineData("Custodian", "/custodian/history?q=zz-none", "No matching records")]
    [InlineData("Custodian", "/custodian/borrowers?q=zz-none", "No matching borrowers")]
    [InlineData("Borrower", "/borrower/history?q=zz-none", "No matching records")]
    public async Task Search_queries_filter_on_the_server_and_show_an_empty_state(string role, string path, string empty)
    {
        var html = await app.Client(role).GetStringAsync(path);
        Assert.Contains(empty, html);
        // The page carries the hooks the automatic search swaps, and no Search button.
        Assert.Contains("data-live-search", html);
        Assert.DoesNotContain(">Search</button>", html);
    }

    [Fact]
    public async Task Demo_data_adds_no_accounts_is_never_duplicated_and_can_be_removed()
    {
        var usersBefore = await app.DbAsync(db => db.Users.CountAsync());
        var reservationsBefore = await app.DbAsync(db => db.Reservations.CountAsync());
        try
        {
            var first = await app.DbAsync(db => DemoData.SeedAsync(db, DateTime.UtcNow));
            Assert.StartsWith("Added 25 demo items", first);
            var second = await app.DbAsync(db => DemoData.SeedAsync(db, DateTime.UtcNow));
            Assert.Equal("Demo data is already present.", second);
            Assert.Equal(usersBefore, await app.DbAsync(db => db.Users.CountAsync()));
            Assert.Equal(25, await app.DbAsync(db => db.EquipmentItems.CountAsync(x => x.AssetCode.StartsWith(DemoData.AssetPrefix))));
            // The test database has no demo-domain accounts, so no activity is attached to anyone's account.
            Assert.Equal(reservationsBefore, await app.DbAsync(db => db.Reservations.CountAsync()));
        }
        finally
        {
            await app.DbAsync(db => DemoData.RemoveAsync(db));
        }
        Assert.Equal(0, await app.DbAsync(db => db.EquipmentItems.CountAsync(x => x.AssetCode.StartsWith(DemoData.AssetPrefix))));
    }
}
