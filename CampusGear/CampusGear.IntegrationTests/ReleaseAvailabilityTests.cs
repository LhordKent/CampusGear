using System.Net;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>
/// WBS 29.0 Equipment Release Recording and 39.0 Availability Status Tracking: release timing rules and the
/// item status flow, run through the real custodian pages. Uses its own app instance and throwaway database.
/// </summary>
public sealed class ReleaseAvailabilityTests(WebAppFixture app) : IClassFixture<WebAppFixture>
{
    private const string OutsideBookingTime = "Release must occur during the approved booking time.";

    // ---- WBS 29.0 Equipment Release Recording ----

    [Fact]
    public async Task Release_before_the_start_time_is_refused()
    {
        var itemId = await app.NewItemAsync();
        var now = DateTime.UtcNow;
        var id = await ApprovedBookingAsync(itemId, now.AddHours(2), now.AddHours(4));

        await AssertReleaseRefusedAsync(id);
    }

    [Fact]
    public async Task Release_after_the_booking_has_ended_is_refused()
    {
        var itemId = await app.NewItemAsync();
        var now = DateTime.UtcNow;
        var id = await ApprovedBookingAsync(itemId, now.AddHours(-4), now.AddHours(-1));

        await AssertReleaseRefusedAsync(id);
    }

    [Fact]
    public async Task Release_during_the_booking_time_is_recorded()
    {
        var itemId = await app.NewItemAsync();
        var now = DateTime.UtcNow;
        var id = await ApprovedBookingAsync(itemId, now.AddMinutes(-30), now.AddHours(2));

        var response = await ReleaseAsync(id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ReservationStatus.Released, await StatusAsync(id));
        Assert.Equal(1, await LoanCountAsync(id));
    }

    // ---- WBS 39.0 Availability Status Tracking ----

    [Fact]
    public async Task Item_status_moves_available_reserved_borrowed_for_repair_and_back_to_available()
    {
        var custodian = app.Client("Custodian");
        var itemId = await app.NewItemAsync();

        // A new unit with no bookings is Available.
        Assert.Equal(ItemStatus.Available, await ItemStatusAsync(itemId));

        // An approved booking whose time has started makes it Reserved.
        var now = DateTime.UtcNow;
        var id = await ApprovedBookingAsync(itemId, now.AddMinutes(-30), now.AddHours(2));
        Assert.Equal(ItemStatus.Reserved, await ItemStatusAsync(itemId));

        // Released to the borrower: Borrowed.
        Assert.Equal(HttpStatusCode.Redirect, (await ReleaseAsync(id)).StatusCode);
        Assert.Equal(ItemStatus.Borrowed, await ItemStatusAsync(itemId));

        // Returned damaged: For repair, with an open maintenance case.
        var returned = await WebAppFixture.PostFormAsync(custodian, $"/custodian/reservations/{id}/return",
            $"/custodian/reservations/{id}", ("condition", "Damaged"), ("notes", "Cracked screen"));
        Assert.Equal(HttpStatusCode.Redirect, returned.StatusCode);
        Assert.Equal(ItemStatus.ForRepair, await ItemStatusAsync(itemId));
        var maintenance = await app.DbAsync(db => db.MaintenanceCases
            .SingleAsync(x => x.EquipmentItemId == itemId && x.Status != MaintenanceStatus.Closed));

        // Repair recorded and the case closed in a usable condition: Available again.
        var closed = await WebAppFixture.PostFormAsync(custodian, $"/admin/maintenance/{maintenance.Id}/update", "/admin/maintenance",
            ("RowVersion", Convert.ToBase64String(maintenance.RowVersion)), ("Status", "Closed"),
            ("Condition", "Good"), ("Resolution", "Screen replaced."));
        Assert.Equal(HttpStatusCode.Redirect, closed.StatusCode);
        Assert.Equal(ItemStatus.Available, await ItemStatusAsync(itemId));
    }

    // ---- Helpers ----

    /// <summary>The custodian's release is refused: the booking stays Approved, no loan is created, and the page explains why.</summary>
    private async Task AssertReleaseRefusedAsync(Guid id)
    {
        var response = await ReleaseAsync(id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ReservationStatus.Approved, await StatusAsync(id));
        Assert.Equal(0, await LoanCountAsync(id));
        var page = await app.Client("Custodian").GetStringAsync(response.Headers.Location!.OriginalString);
        Assert.Contains(OutsideBookingTime, page);
    }

    private Task<Guid> ApprovedBookingAsync(Guid itemId, DateTime startUtc, DateTime endUtc) =>
        app.NewReservationAsync(app.BorrowerA.ProfileId!.Value, itemId, ReservationStatus.Approved, startUtc, endUtc);

    private Task<HttpResponseMessage> ReleaseAsync(Guid id) => WebAppFixture.PostFormAsync(app.Client("Custodian"),
        $"/custodian/reservations/{id}/release", $"/custodian/reservations/{id}", ("condition", "Good"));

    private Task<ReservationStatus> StatusAsync(Guid id) =>
        app.DbAsync(db => db.Reservations.Where(x => x.Id == id).Select(x => x.Status).SingleAsync());

    private Task<int> LoanCountAsync(Guid id) =>
        app.DbAsync(db => db.Loans.CountAsync(x => x.ReservationId == id));

    private Task<ItemStatus> ItemStatusAsync(Guid itemId) =>
        app.DbAsync(async db => (await new CampusGear.Services.EquipmentAvailabilityService(new CampusGear.Data.Repositories.BorrowerReadRepository(db)).GetStatesAsync(DateTime.UtcNow, CancellationToken.None))[itemId]);
}
