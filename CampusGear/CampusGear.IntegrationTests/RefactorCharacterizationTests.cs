using System.Net;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>HTTP contracts frozen before extracting the service/repository boundaries.</summary>
[Collection("Web app permissions")]
public sealed class RefactorCharacterizationTests(WebAppFixture app)
{
    [Fact]
    public async Task Simultaneous_duplicate_quantity_forms_save_one_set_of_units()
    {
        var borrower = await app.NewAccountAsync("Borrower");
        var (client, _) = await app.SignInAsync(borrower.Email);
        using (client)
        {
            var first = await app.NewItemAsync(); var second = await app.NewItemAsync();
            await app.DbAsync(async db =>
            {
                foreach (var item in await db.EquipmentItems.Where(x => x.Id == first || x.Id == second).ToListAsync()) item.Name = "Concurrent batch " + first;
                return await db.SaveChangesAsync();
            });
            var start = DateTime.UtcNow.AddDays(3).AddHours(8);
            var fields = new (string Name, string Value)[]{
                ("RequestId",Guid.NewGuid().ToString()),("EquipmentItemId",first.ToString()),("Quantity","2"),
                ("StartLocal",start.ToString("yyyy-MM-ddTHH:mm")),("EndLocal",start.AddHours(1).ToString("yyyy-MM-ddTHH:mm")),("Purpose","Concurrent form")};
            var responses = await Task.WhenAll(
                WebAppFixture.PostFormAsync(client, "/borrower/reservation", "/borrower/reservation", fields),
                WebAppFixture.PostFormAsync(client, "/borrower/reservation", "/borrower/reservation", fields));
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));
            var rows = await app.DbAsync(db => db.Reservations.Where(x => x.BorrowerProfileId == borrower.ProfileId).ToListAsync());
            Assert.Equal(2, rows.Count); Assert.Equal(2, rows.Select(x => x.RequestId).Distinct().Count());
            Assert.All(rows, row => Assert.Equal(ReservationStatus.Pending, row.Status));
            foreach (var row in rows)
                Assert.Equal(1, await app.DbAsync(db => db.AuditEvents.CountAsync(x => x.EntityId == row.Id.ToString() && x.Action == "reservation.created")));
            // Existing per-unit semantics may return the already-saved subset while the other form is finishing.
            foreach (var response in responses)
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, response.Headers.Location!).Query);
                Assert.NotEmpty(query["id"]);
                Assert.All(query["id"], id => Assert.Contains(rows, row => row.Id.ToString() == id));
            }
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Category_delete_removes_unused_category_but_deactivates_one_with_units(bool hasUnit)
    {
        var id = await app.DbAsync(async db =>
        {
            var row = new EquipmentCategory { Name = "Delete-policy-" + Guid.NewGuid() };
            db.EquipmentCategories.Add(row); await db.SaveChangesAsync(); return row.Id;
        });
        if (hasUnit) await app.NewItemAsync(id);
        var response = await WebAppFixture.PostFormAsync(app.Client("Administrator"), $"/admin/categories/{id}/delete", "/admin/categories");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var category = await app.DbAsync(db => db.EquipmentCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id));
        if (hasUnit) { Assert.NotNull(category); Assert.False(category.IsActive); } else Assert.Null(category);
        Assert.True(await app.DbAsync(db => db.AuditEvents.AnyAsync(x => x.EntityId == id.ToString() &&
            x.Action == (hasUnit ? "category.deactivated" : "category.deleted") && x.ActorUserId == app.Administrator.UserId)));
    }

    [Fact]
    public async Task No_history_account_deletion_removes_profile_and_challenges_but_keeps_old_audit()
    {
        var account = await app.NewAccountAsync("Borrower");
        var auditId = await app.DbAsync(async db =>
        {
            var audit = new AuditEvent { ActorUserId = account.UserId, Action = "fixture.actor", EntityType = "ApplicationUser", EntityId = account.UserId, Outcome = "Succeeded" };
            db.AuditEvents.Add(audit);
            db.EmailChallenges.Add(new()
            {
                UserId = account.UserId,
                Email = account.Email,
                CodeHash = "fixture-only",
                Purpose = EmailChallengePurpose.PasswordReset,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
                MaxAttempts = 5
            });
            await db.SaveChangesAsync(); return audit.Id;
        });
        var response = await WebAppFixture.PostFormAsync(app.Client("Administrator"), $"/admin/users/{account.UserId}/delete", "/admin/users");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await app.DbAsync(db => db.Users.AnyAsync(x => x.Id == account.UserId)));
        Assert.False(await app.DbAsync(db => db.BorrowerProfiles.AnyAsync(x => x.UserId == account.UserId)));
        Assert.False(await app.DbAsync(db => db.EmailChallenges.AnyAsync(x => x.UserId == account.UserId)));
        Assert.Null(await app.DbAsync(db => db.AuditEvents.Where(x => x.Id == auditId).Select(x => x.ActorUserId).SingleAsync()));
        Assert.True(await app.DbAsync(db => db.AuditEvents.AnyAsync(x => x.EntityId == account.UserId && x.Action == "Admin.UserDeleted" && x.ActorUserId == app.Administrator.UserId)));
    }
    [Fact]
    public async Task Stale_user_edit_keeps_latest_details_and_adds_no_update_audit()
    {
        var account = await app.NewAccountAsync("Borrower");
        var profile = await app.DbAsync(db => db.BorrowerProfiles.AsNoTracking().SingleAsync(x => x.UserId == account.UserId));
        var oldStamp = await app.DbAsync(db => db.Users.Where(x => x.Id == account.UserId).Select(x => x.ConcurrencyStamp).SingleAsync());
        await app.DbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == account.UserId);
            user.FirstName = "Latest"; user.ConcurrencyStamp = Guid.NewGuid().ToString();
            return await db.SaveChangesAsync();
        });
        var response = await WebAppFixture.PostFormAsync(app.Client("Administrator"), $"/admin/users/{account.UserId}/edit", $"/admin/users/{account.UserId}/edit",
            ("FirstName", "Stale"), ("LastName", "Editor"), ("Role", "Borrower"), ("IsActive", "true"), ("IsEligible", "true"),
            ("ConcurrencyStamp", oldStamp!), ("ProfileRowVersion", Convert.ToBase64String(profile.RowVersion)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This account changed while the form was open.", await response.Content.ReadAsStringAsync());
        Assert.Equal("Latest", await app.DbAsync(db => db.Users.Where(x => x.Id == account.UserId).Select(x => x.FirstName).SingleAsync()));
        Assert.False(await app.DbAsync(db => db.AuditEvents.AnyAsync(x => x.EntityId == account.UserId && x.Action == "Admin.UserUpdated")));
    }

    [Fact]
    public async Task Stale_maintenance_fetch_keeps_case_open_and_preserves_json_contract()
    {
        var itemId = await app.NewItemAsync();
        var id = await app.DbAsync(async db =>
        {
            var item = await db.EquipmentItems.SingleAsync(x => x.Id == itemId);
            item.IsMaintenanceHold = true;
            var repair = new MaintenanceCase { EquipmentItemId = itemId, Description = "Existing repair", OpenedByUserId = app.Custodian.UserId };
            db.MaintenanceCases.Add(repair); await db.SaveChangesAsync(); return repair.Id;
        });
        var client = app.Client("Custodian");
        var html = await client.GetStringAsync("/admin/maintenance");
        var token = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/maintenance/{id}/update");
        request.Headers.Add("X-Requested-With", "fetch");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["RowVersion"] = Convert.ToBase64String(new byte[8]),
            ["Status"] = "Closed",
            ["Condition"] = "Good",
            ["Resolution"] = "Stale closure"
        });
        var response = await client.SendAsync(request);
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("changed", json.RootElement.GetProperty("message").GetString());
        Assert.True(json.RootElement.TryGetProperty("field", out _));
        Assert.Equal(MaintenanceStatus.Open, await app.DbAsync(db => db.MaintenanceCases.Where(x => x.Id == id).Select(x => x.Status).SingleAsync()));
        Assert.True(await app.DbAsync(db => db.EquipmentItems.Where(x => x.Id == itemId).Select(x => x.IsMaintenanceHold).SingleAsync()));
        Assert.False(await app.DbAsync(db => db.AuditEvents.AnyAsync(x => x.EntityId == id.ToString())));
    }

    [Fact]
    public async Task Delete_preserves_equipment_and_account_with_reservation_history()
    {
        var borrower = await app.NewAccountAsync("Borrower");
        var item = await app.NewItemAsync();
        var record = await app.NewReservationAsync(borrower.ProfileId!.Value, item, ReservationStatus.Cancelled, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
        var admin = app.Client("Administrator");
        Assert.Equal(HttpStatusCode.Redirect, (await WebAppFixture.PostFormAsync(admin, $"/admin/equipment/{item}/delete", "/admin/equipment")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await WebAppFixture.PostFormAsync(admin, $"/admin/users/{borrower.UserId}/delete", "/admin/users")).StatusCode);
        Assert.False(await app.DbAsync(db => db.EquipmentItems.Where(x => x.Id == item).Select(x => x.IsActive).SingleAsync()));
        Assert.False(await app.DbAsync(db => db.Users.Where(x => x.Id == borrower.UserId).Select(x => x.IsActive).SingleAsync()));
        Assert.True(await app.DbAsync(db => db.Reservations.AnyAsync(x => x.Id == record)));
        Assert.True(await app.DbAsync(db => db.BorrowerProfiles.AnyAsync(x => x.Id == borrower.ProfileId)));
        Assert.True(await app.DbAsync(db => db.AuditEvents.AnyAsync(x => x.EntityId == item.ToString() && x.Action == "equipment.deactivated" && x.ActorUserId == app.Administrator.UserId)));
        Assert.True(await app.DbAsync(db => db.AuditEvents.AnyAsync(x => x.EntityId == borrower.UserId && x.Action == "Admin.UserDeactivated")));
    }
    [Fact]
    public async Task Stale_category_edit_keeps_the_saved_name_and_adds_no_audit()
    {
        var id = await app.DbAsync(async db =>
        {
            var category = new EquipmentCategory { Name = "Version " + Guid.NewGuid().ToString("N") };
            db.EquipmentCategories.Add(category);
            await db.SaveChangesAsync();
            return category.Id;
        });
        var before = await app.DbAsync(db => db.EquipmentCategories.AsNoTracking().SingleAsync(x => x.Id == id));
        var response = await WebAppFixture.PostFormAsync(app.Client("Administrator"), "/admin/categories/save", "/admin/categories",
            ("Input.Id", id.ToString()), ("Input.Name", "Must not replace"), ("Input.IsActive", "true"),
            ("Input.RowVersion", Convert.ToBase64String(new byte[8])));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This category changed. Reload before saving.", await response.Content.ReadAsStringAsync());
        Assert.Equal(before.Name, await app.DbAsync(db => db.EquipmentCategories.Where(x => x.Id == id).Select(x => x.Name).SingleAsync()));
        Assert.False(await app.DbAsync(db => db.AuditEvents.AnyAsync(x => x.EntityId == id.ToString())));
    }

    [Fact]
    public async Task Stale_inventory_edit_keeps_the_asset_and_adds_no_audit()
    {
        var id = await app.NewItemAsync();
        var response = await WebAppFixture.PostFormAsync(app.Client("Administrator"), "/admin/equipment/save", "/admin/equipment",
            ("Input.Id", id.ToString()), ("Input.CategoryId", app.CategoryId.ToString()), ("Input.AssetCode", "Not-saved"),
            ("Input.Name", "Must not replace"), ("Input.Condition", "Good"), ("Input.IsActive", "true"),
            ("Input.RowVersion", Convert.ToBase64String(new byte[8])));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This equipment changed. Reload it before saving.", await response.Content.ReadAsStringAsync());
        Assert.NotEqual("Not-saved", await app.DbAsync(db => db.EquipmentItems.Where(x => x.Id == id).Select(x => x.AssetCode).SingleAsync()));
        Assert.False(await app.DbAsync(db => db.AuditEvents.AnyAsync(x => x.EntityId == id.ToString())));
    }

    [Fact]
    public async Task Repeated_multiunit_form_returns_the_same_reservations()
    {
        var borrower = await app.NewAccountAsync("Borrower");
        var (client, _) = await app.SignInAsync(borrower.Email);
        using (client)
        {
            var first = await app.NewItemAsync();
            var second = await app.NewItemAsync();
            await app.DbAsync(async db =>
            {
                var items = await db.EquipmentItems.Where(x => x.Id == first || x.Id == second).ToListAsync();
                foreach (var item in items) item.Name = "Quantity " + first;
                return await db.SaveChangesAsync();
            });
            var start = DateTime.UtcNow.AddDays(2).AddHours(8);
            var fields = new (string Name, string Value)[]
            {
                ("RequestId", Guid.NewGuid().ToString()), ("EquipmentItemId", first.ToString()), ("Quantity", "2"),
                ("StartLocal", start.ToString("yyyy-MM-ddTHH:mm")), ("EndLocal", start.AddHours(2).ToString("yyyy-MM-ddTHH:mm")),
                ("Purpose", "Quantity characterization")
            };
            var one = await WebAppFixture.PostFormAsync(client, "/borrower/reservation", "/borrower/reservation", fields);
            var two = await WebAppFixture.PostFormAsync(client, "/borrower/reservation", "/borrower/reservation", fields);
            Assert.Equal(HttpStatusCode.Redirect, one.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, two.StatusCode);
            Assert.Equal(one.Headers.Location!.OriginalString.Split('&').Order(),
                two.Headers.Location!.OriginalString.Split('&').Order());
            var rows = await app.DbAsync(db => db.Reservations.Where(x => x.BorrowerProfileId == borrower.ProfileId).ToListAsync());
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.Equal(ReservationStatus.Pending, row.Status));
            foreach (var row in rows)
                Assert.True(await app.DbAsync(db => db.AuditEvents.AnyAsync(x => x.EntityId == row.Id.ToString() &&
                    x.Action == "reservation.created" && x.ActorUserId == borrower.UserId)));
        }
    }
}
