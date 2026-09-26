using CampusGear.Models;
using CampusGear.Services.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CampusGear.Data;

namespace CampusGear.IntegrationTests;

[Collection("SQL reservation lifecycle")]
public sealed class ReservationServiceSqlTests(SqlReservationFixture fixture)
{
    private static readonly DateTime Baseline = new(2030, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Simultaneous_overlapping_requests_have_exactly_one_winner()
    {
        fixture.Clock.Set(Baseline);
        var first = await fixture.NewBorrowerAsync();
        var second = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = SubmitAfterGateAsync(gate.Task, first, Command(first, item, Baseline.AddDays(2), Baseline.AddDays(2).AddHours(2)));
        var b = SubmitAfterGateAsync(gate.Task, second, Command(second, item, Baseline.AddDays(2).AddHours(1), Baseline.AddDays(2).AddHours(3)));
        gate.SetResult(true);
        var outcomes = await Task.WhenAll(a, b);
        Assert.Single(outcomes.Where(x => x.Result is not null));
        Assert.Equal(ReservationErrorCode.ScheduleConflict, Assert.Single(outcomes.Where(x => x.Error is not null)).Error);
        Assert.Equal(1, await fixture.DbAsync(db => db.Reservations.CountAsync(x => x.EquipmentItemId == item)));
    }

    [Fact]
    public async Task Duplicate_concurrent_request_ids_reuse_one_record_and_one_audit_event()
    {
        fixture.Clock.Set(Baseline);
        var borrower = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        var command = Command(borrower, item, Baseline.AddDays(2), Baseline.AddDays(2).AddHours(2));
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = SubmitAfterGateAsync(gate.Task, borrower, command);
        var b = SubmitAfterGateAsync(gate.Task, borrower, command);
        gate.SetResult(true);
        var results = await Task.WhenAll(a, b);
        Assert.All(results, x => Assert.Null(x.Error));
        Assert.Equal(results[0].Result!.Id, results[1].Result!.Id);
        Assert.Equal(1, await fixture.DbAsync(db => db.Reservations.CountAsync(x => x.RequestId == command.RequestId)));
        var entityId = results[0].Result!.Id.ToString("D");
        Assert.Equal(1, await fixture.DbAsync(db => db.AuditEvents.CountAsync(x => x.EntityId == entityId && x.Action == "reservation.created")));
        fixture.Clock.Set(command.EndAtUtc.AddDays(1));
        var laterReplay = await CreateAsync(borrower, command);
        Assert.Equal(results[0].Result!.Id, laterReplay.Id);
    }

    [Fact]
    public async Task Half_open_adjacent_slots_are_allowed()
    {
        fixture.Clock.Set(Baseline);
        var borrower = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        var start = Baseline.AddDays(2);
        await CreateAsync(borrower, Command(borrower, item, start, start.AddHours(1)));
        await CreateAsync(borrower, Command(borrower, item, start.AddHours(1), start.AddHours(2)));
        Assert.Equal(2, await fixture.DbAsync(db => db.Reservations.CountAsync(x => x.EquipmentItemId == item)));
    }

    [Fact]
    public async Task Hold_expires_at_24_hours_or_start_and_frees_the_slot()
    {
        fixture.Clock.Set(Baseline);
        var borrower = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        var longStart = Baseline.AddDays(2);
        var held = await CreateAsync(borrower, Command(borrower, item, longStart, longStart.AddHours(1)));
        Assert.Equal(Baseline.AddHours(24), held.HoldExpiresAtUtc);
        fixture.Clock.Set(Baseline.AddHours(24).AddMinutes(1));
        Assert.True(await fixture.ServiceAsync(service => service.ExpireDueAsync()) >= 1);
        Assert.Equal(ReservationStatus.Expired, await fixture.DbAsync(db => db.Reservations
            .Where(x => x.Id == held.Id).Select(x => x.Status).SingleAsync()));
        await CreateAsync(borrower, Command(borrower, item, longStart, longStart.AddHours(1)));

        fixture.Clock.Set(Baseline);
        var shortItem = await fixture.NewItemAsync();
        var shortStart = Baseline.AddHours(2);
        var shortHold = await CreateAsync(borrower, Command(borrower, shortItem, shortStart, shortStart.AddHours(1)));
        Assert.Equal(shortStart, shortHold.HoldExpiresAtUtc);
        fixture.Clock.Set(shortStart);
        var approval = await fixture.ServiceAsync(service => service.ApproveAsync(Staff(), shortHold.Id));
        Assert.Equal(ReservationStatus.Expired, approval.Status);
    }

    [Fact]
    public async Task Malformed_or_start_passed_pending_rows_do_not_block_and_are_expired()
    {
        fixture.Clock.Set(Baseline);
        var borrower = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        var malformedId = await fixture.DbAsync(async db =>
        {
            var malformed = new Reservation
            {
                BorrowerProfileId = borrower.ProfileId, EquipmentItemId = item,
                StartAtUtc = Baseline.AddHours(1), EndAtUtc = Baseline.AddHours(3),
                HoldExpiresAtUtc = null, Purpose = "Test malformed pending hold"
            };
            db.Reservations.Add(malformed);
            await db.SaveChangesAsync();
            return malformed.Id;
        });
        await CreateAsync(borrower, Command(borrower, item, Baseline.AddHours(1), Baseline.AddHours(2)));
        await fixture.ServiceAsync(service => service.ExpireDueAsync());
        Assert.Equal(ReservationStatus.Expired, await fixture.DbAsync(db => db.Reservations
            .Where(x => x.Id == malformedId).Select(x => x.Status).SingleAsync()));

        var pastItem = await fixture.NewItemAsync();
        var pastId = await fixture.DbAsync(async db =>
        {
            var malformed = new Reservation
            {
                BorrowerProfileId = borrower.ProfileId, EquipmentItemId = pastItem,
                StartAtUtc = Baseline.AddHours(-1), EndAtUtc = Baseline.AddHours(3),
                HoldExpiresAtUtc = Baseline.AddHours(2), Purpose = "Test elapsed start"
            };
            db.Reservations.Add(malformed);
            await db.SaveChangesAsync();
            return malformed.Id;
        });
        await CreateAsync(borrower, Command(borrower, pastItem, Baseline.AddMinutes(30), Baseline.AddHours(1)));
        await fixture.ServiceAsync(service => service.ExpireDueAsync());
        Assert.Equal(ReservationStatus.Expired, await fixture.DbAsync(db => db.Reservations
            .Where(x => x.Id == pastId).Select(x => x.Status).SingleAsync()));
    }

    [Fact]
    public async Task Ownership_and_staff_role_are_enforced_and_cancellation_frees_the_slot()
    {
        fixture.Clock.Set(Baseline);
        var owner = await fixture.NewBorrowerAsync();
        var other = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        var command = Command(owner, item, Baseline.AddDays(2), Baseline.AddDays(2).AddHours(1));
        var request = await CreateAsync(owner, command);
        var cancelError = await Assert.ThrowsAsync<ReservationWorkflowException>(() => fixture.ServiceAsync(
            service => service.CancelAsync(new(other.UserId), request.Id)));
        Assert.Equal(ReservationErrorCode.Forbidden, cancelError.Code);
        var approveError = await Assert.ThrowsAsync<ReservationWorkflowException>(() => fixture.ServiceAsync(
            service => service.ApproveAsync(new(owner.UserId), request.Id)));
        Assert.Equal(ReservationErrorCode.Forbidden, approveError.Code);
        var createError = await Assert.ThrowsAsync<ReservationWorkflowException>(() => fixture.ServiceAsync(
            service => service.CreateAsync(Staff(), command with { RequestId = Guid.NewGuid() })));
        Assert.Equal(ReservationErrorCode.Forbidden, createError.Code);

        var cancelled = await fixture.ServiceAsync(service => service.CancelAsync(new(owner.UserId), request.Id));
        Assert.Equal(ReservationStatus.Cancelled, cancelled.Status);
        await CreateAsync(other, command with { RequestId = Guid.NewGuid(), BorrowerUserId = other.UserId });
    }

    [Fact]
    public async Task Approval_and_release_recheck_current_borrower_eligibility()
    {
        fixture.Clock.Set(Baseline);
        var borrower = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        var start = Baseline.AddHours(1);
        var request = await CreateAsync(borrower, Command(borrower, item, start, start.AddHours(2)));
        await SetEligibilityAsync(borrower, false);
        var approvalError = await Assert.ThrowsAsync<ReservationWorkflowException>(() => fixture.ServiceAsync(
            service => service.ApproveAsync(Staff(), request.Id)));
        Assert.Equal(ReservationErrorCode.IneligibleBorrower, approvalError.Code);
        await SetEligibilityAsync(borrower, true);
        await fixture.ServiceAsync(service => service.ApproveAsync(Staff(), request.Id));
        fixture.Clock.Set(start.AddMinutes(1));
        await SetEligibilityAsync(borrower, false);
        var releaseError = await Assert.ThrowsAsync<ReservationWorkflowException>(() => fixture.ServiceAsync(
            service => service.ReleaseAsync(Staff(), request.Id, EquipmentCondition.Good)));
        Assert.Equal(ReservationErrorCode.IneligibleBorrower, releaseError.Code);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("unconfirmed")]
    [InlineData("role-removed")]
    public async Task Approval_rechecks_current_account_and_borrower_role(string change)
    {
        fixture.Clock.Set(Baseline);
        var borrower = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        var request = await CreateAsync(borrower, Command(borrower, item, Baseline.AddDays(2), Baseline.AddDays(2).AddHours(1)));
        await fixture.DbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == borrower.UserId);
            if (change == "inactive") user.IsActive = false;
            if (change == "unconfirmed") user.EmailConfirmed = false;
            if (change == "role-removed")
            {
                var borrowerRoleId = await db.Roles.Where(x => x.NormalizedName == "BORROWER").Select(x => x.Id).SingleAsync();
                db.UserRoles.Remove(await db.UserRoles.SingleAsync(x => x.UserId == borrower.UserId && x.RoleId == borrowerRoleId));
            }
            return await db.SaveChangesAsync();
        });
        var error = await Assert.ThrowsAsync<ReservationWorkflowException>(() => fixture.ServiceAsync(
            service => service.ApproveAsync(Staff(), request.Id)));
        Assert.Equal(ReservationErrorCode.IneligibleBorrower, error.Code);
    }

    [Fact]
    public async Task Eligibility_change_while_create_waits_for_profile_lock_uses_fresh_account_data()
    {
        fixture.Clock.Set(Baseline);
        var borrower = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var resource = $"CampusGear:BorrowerProfile:{borrower.ProfileId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 50000, 'Test lock failed', 1;");
        var attempted = fixture.LockObserver.Arm(resource);
        var creating = CreateAsync(borrower, Command(borrower, item, Baseline.AddDays(2), Baseline.AddDays(2).AddHours(1)));
        try
        {
            await attempted.WaitAsync(TimeSpan.FromSeconds(10));
            var user = await db.Users.SingleAsync(x => x.Id == borrower.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            var error = await Assert.ThrowsAsync<ReservationWorkflowException>(() => creating);
            Assert.Equal(ReservationErrorCode.IneligibleBorrower, error.Code);
        }
        finally { fixture.LockObserver.Disarm(); }
    }

    [Fact]
    public async Task Release_and_late_damaged_return_are_idempotent_and_create_a_maintenance_hold()
    {
        fixture.Clock.Set(Baseline);
        var borrower = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        var start = Baseline.AddHours(1);
        var end = start.AddHours(2);
        var request = await CreateAsync(borrower, Command(borrower, item, start, end));
        await fixture.ServiceAsync(service => service.ApproveAsync(Staff(), request.Id));
        var earlyError = await Assert.ThrowsAsync<ReservationWorkflowException>(() => fixture.ServiceAsync(
            service => service.ReleaseAsync(Staff(), request.Id, EquipmentCondition.Good)));
        Assert.Equal(ReservationErrorCode.InvalidState, earlyError.Code);
        fixture.Clock.Set(start.AddMinutes(1));
        var released = await fixture.ServiceAsync(service => service.ReleaseAsync(Staff(), request.Id, EquipmentCondition.Good));
        var replay = await fixture.ServiceAsync(service => service.ReleaseAsync(Staff(), request.Id, EquipmentCondition.Good));
        Assert.Equal(released.LoanId, replay.LoanId);
        Assert.Equal(1, await fixture.DbAsync(db => db.Loans.CountAsync(x => x.ReservationId == request.Id)));

        fixture.Clock.Set(end.AddMinutes(10));
        var returned = await fixture.ServiceAsync(service => service.ReturnAsync(Staff(), request.Id, EquipmentCondition.Damaged, "Cracked casing"));
        Assert.Equal(ReservationStatus.Completed, returned.Status);
        Assert.True(returned.ReturnedAtUtc > returned.DueAtUtc);
        Assert.Equal(EquipmentCondition.Damaged, returned.ConditionAtReturn);
        await fixture.ServiceAsync(service => service.ReturnAsync(Staff(), request.Id, EquipmentCondition.Damaged));
        Assert.True(await fixture.DbAsync(db => db.EquipmentItems.Where(x => x.Id == item).Select(x => x.IsMaintenanceHold).SingleAsync()));
        Assert.Equal(1, await fixture.DbAsync(db => db.MaintenanceCases.CountAsync(x => x.LoanId == released.LoanId)));
        var blocked = await Assert.ThrowsAsync<ReservationWorkflowException>(() => CreateAsync(borrower,
            Command(borrower, item, end.AddDays(1), end.AddDays(1).AddHours(1))));
        Assert.Equal(ReservationErrorCode.UnavailableEquipment, blocked.Code);
    }

    [Fact]
    public async Task Outstanding_previous_loan_blocks_release_of_adjacent_booking_until_returned()
    {
        fixture.Clock.Set(Baseline);
        var borrower = await fixture.NewBorrowerAsync();
        var item = await fixture.NewItemAsync();
        var start = Baseline.AddHours(1);
        var first = await CreateAsync(borrower, Command(borrower, item, start, start.AddHours(1)));
        var next = await CreateAsync(borrower, Command(borrower, item, start.AddHours(1), start.AddHours(2)));
        await fixture.ServiceAsync(service => service.ApproveAsync(Staff(), first.Id));
        await fixture.ServiceAsync(service => service.ApproveAsync(Staff(), next.Id));
        fixture.Clock.Set(start.AddMinutes(1));
        await fixture.ServiceAsync(service => service.ReleaseAsync(Staff(), first.Id, EquipmentCondition.Good));
        fixture.Clock.Set(start.AddHours(1).AddMinutes(1));
        var error = await Assert.ThrowsAsync<ReservationWorkflowException>(() => fixture.ServiceAsync(
            service => service.ReleaseAsync(Staff(), next.Id, EquipmentCondition.Good)));
        Assert.Equal(ReservationErrorCode.ScheduleConflict, error.Code);
        await fixture.ServiceAsync(service => service.ReturnAsync(Staff(), first.Id, EquipmentCondition.Good));
        var released = await fixture.ServiceAsync(service => service.ReleaseAsync(Staff(), next.Id, EquipmentCondition.Good));
        Assert.Equal(ReservationStatus.Released, released.Status);
    }

    private ReservationActor Staff() => new(fixture.StaffUserId);
    private Task<ReservationResult> CreateAsync(TestBorrower borrower, CreateReservationCommand command) =>
        fixture.ServiceAsync(service => service.CreateAsync(new(borrower.UserId), command));

    private static CreateReservationCommand Command(TestBorrower borrower, Guid item, DateTime start, DateTime end) =>
        new(Guid.NewGuid(), borrower.UserId, item, start, end, "Integration test request");

    private async Task<SubmissionOutcome> SubmitAfterGateAsync(Task gate, TestBorrower borrower, CreateReservationCommand command)
    {
        await gate;
        try { return new(await CreateAsync(borrower, command), null); }
        catch (ReservationWorkflowException exception) { return new(null, exception.Code); }
    }

    private Task<int> SetEligibilityAsync(TestBorrower borrower, bool eligible) => fixture.DbAsync(async db =>
    {
        var profile = await db.BorrowerProfiles.SingleAsync(x => x.Id == borrower.ProfileId);
        profile.IsEligible = eligible;
        return await db.SaveChangesAsync();
    });

    private sealed record SubmissionOutcome(ReservationResult? Result, ReservationErrorCode? Error);
}
