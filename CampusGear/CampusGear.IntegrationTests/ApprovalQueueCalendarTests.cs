
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampusGear.IntegrationTests;

[Collection("Web app permissions")]
public sealed class ApprovalQueueCalendarTests(WebAppFixture app)
{
    [Fact]
    public async Task ApprovalQueue_DateFiltersOnlyShowReservationsWithinSelectedRange()
    {
        var firstItemId = await app.NewItemAsync();
        var secondItemId = await app.NewItemAsync();

        var firstStart = DateTime.UtcNow.AddDays(14);
        var secondStart = firstStart.AddDays(10);

        await app.NewReservationAsync(
            app.BorrowerA.ProfileId!.Value,
            firstItemId,
            ReservationStatus.Approved,
            firstStart,
            firstStart.AddHours(2));

        await app.NewReservationAsync(
            app.BorrowerB.ProfileId!.Value,
            secondItemId,
            ReservationStatus.Approved,
            secondStart,
            secondStart.AddHours(2));

        var firstItemName = await app.DbAsync(db =>
            db.EquipmentItems
                .Where(x => x.Id == firstItemId)
                .Select(x => x.Name)
                .SingleAsync());

        var secondItemName = await app.DbAsync(db =>
            db.EquipmentItems
                .Where(x => x.Id == secondItemId)
                .Select(x => x.Name)
                .SingleAsync());

        // Convert the UTC reservation date to Manila's UTC+8 date.
        var selectedDate = firstStart.AddHours(8).ToString("yyyy-MM-dd");

        var response = await app.Client("Custodian").GetAsync(
            $"/custodian/approvals?status=approved&from={selectedDate}&to={selectedDate}");

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(firstItemName, html);
        Assert.DoesNotContain(secondItemName, html);
    }

    [Theory]
    [InlineData(
        "not-a-date",
        "",
        "Use valid dates for the date filters.")]
    [InlineData(
        "1999-12-31",
        "",
        "Choose filter dates between 2000 and 2100.")]
    [InlineData(
        "2026-11-20",
        "2026-11-19",
        "The end date must be on or after the start date.")]
    public async Task ApprovalQueue_InvalidDateFiltersShowValidationMessage(
        string from,
        string to,
        string expectedMessage)
    {
        var response = await app.Client("Custodian").GetAsync(
            $"/custodian/approvals?status=approved&from={from}&to={to}");

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(expectedMessage, html);
    }

    [Fact]
    public async Task Calendar_ShowsBookingAndBorrowerNameForSelectedDay()
    {
        var itemId = await app.NewItemAsync();

        var startUtc = DateTime.UtcNow.AddDays(14);
        var endUtc = startUtc.AddHours(2);
        var selectedDate = startUtc.AddHours(8).Date;

        await app.NewReservationAsync(
            app.BorrowerA.ProfileId!.Value,
            itemId,
            ReservationStatus.Approved,
            startUtc,
            endUtc);

        var borrowerName = await app.DbAsync(db =>
            db.BorrowerProfiles
                .Where(x => x.Id == app.BorrowerA.ProfileId!.Value)
                .Select(x => x.User.FullName)
                .SingleAsync());

        var itemName = await app.DbAsync(db =>
            db.EquipmentItems
                .Where(x => x.Id == itemId)
                .Select(x => x.Name)
                .SingleAsync());

        var response = await app.Client("Custodian").GetAsync(
            $"/custodian/calendar?year={selectedDate.Year}" +
            $"&month={selectedDate.Month}" +
            $"&equipmentItemId={itemId}" +
            $"&day={selectedDate:yyyy-MM-dd}");

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(itemName, html);
        Assert.Contains(borrowerName, html);
        Assert.Contains("1 booking", html);
    }
}
