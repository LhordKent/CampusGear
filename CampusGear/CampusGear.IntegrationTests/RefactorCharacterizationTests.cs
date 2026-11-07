using System.Net;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>HTTP contracts frozen before extracting the service/repository boundaries.</summary>
[Collection("Web app permissions")]
public sealed class RefactorCharacterizationTests(WebAppFixture app)
{
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
