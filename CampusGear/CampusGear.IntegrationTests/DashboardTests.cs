
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>
/// WBS 7.0, 8.0 and 9.0: verifies role dashboard data and activity feeds.
/// </summary>
[Collection("Web app permissions")]
public sealed class DashboardTests(WebAppFixture app)
{
    [Fact]
    public async Task Borrower_dashboard_shows_own_pending_reservation()
    {
        var itemId = await app.NewItemAsync();
        var now = DateTime.UtcNow;
        var reservationId = await app.NewReservationAsync(
            app.BorrowerA.ProfileId!.Value,
            itemId,
            ReservationStatus.Pending,
            now.AddDays(2),
            now.AddDays(2).AddHours(2));

        var item = await app.DbAsync(db => db.EquipmentItems
            .Where(x => x.Id == itemId)
            .Select(x => new { x.Name })
            .SingleAsync());

        var html = await app.Client("Borrower").GetStringAsync("/borrower/home");

        Assert.Contains(item.Name, html);
        Assert.Contains("Pending", html);

        // The reservation must belong to this borrower.
        Assert.True(await app.DbAsync(db => db.Reservations.AnyAsync(
            x => x.Id == reservationId &&
                 x.BorrowerProfileId == app.BorrowerA.ProfileId)));
    }

    [Fact]
    public async Task Custodian_dashboard_shows_pending_request()
    {
        var itemId = await app.NewItemAsync();
        var now = DateTime.UtcNow;

        await app.NewReservationAsync(
            app.BorrowerA.ProfileId!.Value,
            itemId,
            ReservationStatus.Pending,
            now.AddDays(2),
            now.AddDays(2).AddHours(2));

        var item = await app.DbAsync(db => db.EquipmentItems
            .Where(x => x.Id == itemId)
            .Select(x => new { x.Name })
            .SingleAsync());

        var html = await app.Client("Custodian").GetStringAsync("/custodian/dashboard");

        Assert.Contains(item.Name, html);
        Assert.Contains("Pending", html);
    }

    [Fact]
    public async Task Administrator_dashboard_shows_pending_registration()
    {
        var borrower = await app.NewAccountAsync("Borrower");

        await app.DbAsync(db => db.Users
            .Where(x => x.Id == borrower.UserId)
            .ExecuteUpdateAsync(x => x.SetProperty(
                user => user.ApprovalStatus,
                AccountApprovalStatus.PendingApproval)));

        var html = await app.Client("Administrator").GetStringAsync("/admin/dashboard");

        Assert.Contains(borrower.Email, html);
        Assert.Contains("Pending approval", html);
    }
}
