using System.Net;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>
/// WBS 31.0 Return Recording, 32.0 Late Return and 33.0 Condition Check: the custodian records a return through the
/// reservation page, and the loan, the equipment and its maintenance record are updated to match.
/// </summary>
[Collection("Web app permissions")]
public sealed class ReturnConditionCheckTests(WebAppFixture app)
{
    /// <summary>
    /// An item that is out on loan: a released reservation with its loan record, released an hour ago and due back at
    /// <paramref name="dueUtc"/>.
    /// </summary>
    private async Task<(Guid ReservationId, Guid ItemId, Guid LoanId)> OnLoanAsync(DateTime dueUtc)
    {
        var releasedUtc = DateTime.UtcNow.AddHours(-1);
        var itemId = await app.NewItemAsync();
        var reservationId = await app.NewReservationAsync(app.BorrowerA.ProfileId!.Value, itemId,
            ReservationStatus.Released, releasedUtc, dueUtc);
        var loanId = await app.DbAsync(async db =>
        {
            var loan = new Loan
            {
                ReservationId = reservationId, ReleasedAtUtc = releasedUtc, DueAtUtc = dueUtc,
                ReleasedByUserId = app.Custodian.UserId, ConditionAtRelease = EquipmentCondition.Good
            };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();
            return loan.Id;
        });
        return (reservationId, itemId, loanId);
    }

    private Task<HttpResponseMessage> ReturnAsync(Guid reservationId, string condition, string? notes = null)
    {
        var fields = new List<(string, string)> { ("condition", condition) };
        if (notes is not null) fields.Add(("notes", notes));
        return WebAppFixture.PostFormAsync(app.Client("Custodian"), $"/custodian/reservations/{reservationId}/return",
            $"/custodian/reservations/{reservationId}", [.. fields]);
    }

    /// <summary>The reservation page the custodian is sent back to, with the success or error message on it.</summary>
    private Task<string> FollowAsync(HttpResponseMessage response) =>
        app.Client("Custodian").GetStringAsync(response.Headers.Location!.OriginalString);

    private Task<Loan> LoanAsync(Guid loanId) =>
        app.DbAsync(db => db.Loans.AsNoTracking().SingleAsync(x => x.Id == loanId));

    // WBS 31.0 — Return Recording

    [Fact]
    public async Task Return_InGoodCondition_CompletesTheLoan()
    {
        var (reservationId, itemId, loanId) = await OnLoanAsync(DateTime.UtcNow.AddHours(2));

        var response = await ReturnAsync(reservationId, "Good", "All parts present.");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Return recorded. The equipment condition and borrowing history are updated.", await FollowAsync(response));
        var loan = await LoanAsync(loanId);
        Assert.NotNull(loan.ReturnedAtUtc);
        Assert.Equal(app.Custodian.UserId, loan.ReceivedByUserId);
        Assert.Equal(EquipmentCondition.Good, loan.ConditionAtReturn);
        Assert.Equal("All parts present.", loan.ReturnNotes);
        var (status, onHold, anyCase) = await app.DbAsync(async db => (
            await db.Reservations.Where(x => x.Id == reservationId).Select(x => x.Status).SingleAsync(),
            await db.EquipmentItems.Where(x => x.Id == itemId).Select(x => x.IsMaintenanceHold).SingleAsync(),
            await db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == itemId)));
        Assert.Equal(ReservationStatus.Completed, status);
        Assert.False(onHold);
        Assert.False(anyCase);
    }

    [Fact]
    public async Task Return_WithUnknownCondition_IsRefused()
    {
        var (reservationId, _, loanId) = await OnLoanAsync(DateTime.UtcNow.AddHours(2));

        var response = await ReturnAsync(reservationId, "Broken");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Choose a valid return condition.", await FollowAsync(response));
        Assert.Null((await LoanAsync(loanId)).ReturnedAtUtc);
    }

    [Fact]
    public async Task Return_WithNotesOver2000Characters_IsRefused()
    {
        var (reservationId, _, loanId) = await OnLoanAsync(DateTime.UtcNow.AddHours(2));

        var response = await ReturnAsync(reservationId, "Good", new string('x', 2001));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Return notes must use 2000 characters or fewer.", await FollowAsync(response));
        var loan = await LoanAsync(loanId);
        Assert.Null(loan.ReturnedAtUtc);
        Assert.Null(loan.ReturnNotes);
    }

    [Fact]
    public async Task Return_WithNotesOfExactly2000Characters_IsAccepted()
    {
        var (reservationId, _, loanId) = await OnLoanAsync(DateTime.UtcNow.AddHours(2));
        var notes = new string('x', 2000);

        var response = await ReturnAsync(reservationId, "Good", notes);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(notes, (await LoanAsync(loanId)).ReturnNotes);
    }

    [Fact]
    public async Task Return_OfALoanAlreadyReturned_DoesNotChangeIt()
    {
        var (reservationId, _, loanId) = await OnLoanAsync(DateTime.UtcNow.AddHours(2));
        await ReturnAsync(reservationId, "Good", "First return.");
        var first = await LoanAsync(loanId);

        // A second submission, e.g. the custodian pressing the button twice, must not overwrite the first record.
        var response = await ReturnAsync(reservationId, "Damaged", "Second return.");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var loan = await LoanAsync(loanId);
        Assert.Equal(first.ReturnedAtUtc, loan.ReturnedAtUtc);
        Assert.Equal(EquipmentCondition.Good, loan.ConditionAtReturn);
        Assert.Equal("First return.", loan.ReturnNotes);
    }

    // WBS 32.0 — Late Return

    [Fact]
    public async Task Return_AfterTheDueTime_IsMarkedReturnedLate()
    {
        var (reservationId, _, loanId) = await OnLoanAsync(DateTime.UtcNow.AddMinutes(-30));

        var response = await ReturnAsync(reservationId, "Good");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var loan = await LoanAsync(loanId);
        Assert.True(loan.ReturnedAtUtc > loan.DueAtUtc);
        Assert.Contains("Returned late", await FollowAsync(response));
    }

    [Fact]
    public async Task Return_BeforeTheDueTime_IsNotMarkedLate()
    {
        var (reservationId, _, _) = await OnLoanAsync(DateTime.UtcNow.AddHours(2));

        var response = await ReturnAsync(reservationId, "Good");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("Returned late", await FollowAsync(response));
    }

    // WBS 33.0 — Condition Check

    [Theory]
    [InlineData("Damaged")]
    [InlineData("Unserviceable")]
    public async Task Return_DamagedOrUnserviceable_CreatesMaintenanceHold(string condition)
    {
        var (reservationId, itemId, loanId) = await OnLoanAsync(DateTime.UtcNow.AddHours(2));

        var response = await ReturnAsync(reservationId, condition, "Cracked casing.");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("A maintenance hold was created for this equipment.", await FollowAsync(response));
        var (item, cases) = await app.DbAsync(async db => (
            await db.EquipmentItems.AsNoTracking().SingleAsync(x => x.Id == itemId),
            await db.MaintenanceCases.Where(x => x.EquipmentItemId == itemId).ToListAsync()));
        Assert.True(item.IsMaintenanceHold);
        Assert.Equal(Enum.Parse<EquipmentCondition>(condition), item.Condition);
        var maintenance = Assert.Single(cases);
        Assert.Equal(MaintenanceStatus.Open, maintenance.Status);
        Assert.Equal(loanId, maintenance.LoanId);
    }

    [Theory]
    [InlineData("Excellent")]
    [InlineData("Good")]
    [InlineData("Fair")]
    public async Task Return_InUsableCondition_CreatesNoMaintenanceHold(string condition)
    {
        var (reservationId, itemId, _) = await OnLoanAsync(DateTime.UtcNow.AddHours(2));

        var response = await ReturnAsync(reservationId, condition);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("A maintenance hold was created", await FollowAsync(response));
        var (item, anyCase) = await app.DbAsync(async db => (
            await db.EquipmentItems.AsNoTracking().SingleAsync(x => x.Id == itemId),
            await db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == itemId)));
        Assert.False(item.IsMaintenanceHold);
        Assert.Equal(Enum.Parse<EquipmentCondition>(condition), item.Condition);
        Assert.False(anyCase);
    }
}
