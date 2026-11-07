using CampusGear.Data;
using CampusGear.Data.Interfaces;
using CampusGear.Models;
using CampusGear.Services;
using CampusGear.Services.Reservations;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace CampusGear.IntegrationTests;
[Collection("Web app permissions")]
public sealed class BorrowerOrchestrationTests(WebAppFixture app)
{
    [Theory]
    [InlineData("retry")]
    [InlineData("forbidden")]
    [InlineData("disconnect")]
    public async Task Quantity_workflow_preserves_candidate_retry_compensation_and_disconnect_behavior(string mode)
    {
        var account = await app.NewAccountAsync("Borrower");
        var ids = new[] { await app.NewItemAsync(), await app.NewItemAsync(), await app.NewItemAsync() };
        await app.DbAsync(async db =>
        {
            var rows = await db.EquipmentItems.Where(x => ids.Contains(x.Id)).ToListAsync();
            foreach (var row in rows) row.Name = "Batch " + ids[0]; return await db.SaveChangesAsync();
        });
        await using var scope = app.Services.CreateAsyncScope();
        using var disconnect = new CancellationTokenSource();
        var wrapper = new InterceptedReservations(scope.ServiceProvider.GetRequiredService<IReservationService>(), mode, disconnect);
        var service = new BorrowerService(scope.ServiceProvider.GetRequiredService<IBorrowerReadRepository>(),
        scope.ServiceProvider.GetRequiredService<IEquipmentAvailabilityService>(),
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), wrapper, TimeProvider.System);
        var start = DateTime.UtcNow.AddDays(2).AddHours(8);
        var result = await service.CreateReservationAsync(new(account.UserId, "127.0.0.1", "batch-check"),
        new()
        {
            RequestId = Guid.NewGuid(),
            EquipmentItemId = ids[0],
            Quantity = 2,
            StartLocal = start.ToString("yyyy-MM-ddTHH:mm"),
            EndLocal = start.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
            Purpose = "Workflow characterization"
        }, disconnect.Token);
        var rows = await app.DbAsync(db => db.Reservations.Where(x => x.BorrowerProfileId == account.ProfileId).ToListAsync());
        Assert.All(wrapper.CreateTokens, ct => Assert.False(ct.CanBeCanceled));
        if (mode == "forbidden")
        {
            Assert.Equal(OperationOutcome.Forbidden, result.Outcome);
            Assert.Equal(ReservationStatus.Cancelled, Assert.Single(rows).Status);
            Assert.Equal(1, wrapper.Cancellations);
        }
        else
        {
            Assert.True(result.Succeeded);
            Assert.Equal(2, rows.Count); Assert.Equal(2, result.Value!.CompletedIds.Count);
            Assert.All(rows, x => Assert.Equal(ReservationStatus.Pending, x.Status));
            if (mode == "retry") Assert.Equal(3, wrapper.CreateTokens.Count);
            if (mode == "disconnect") Assert.True(disconnect.IsCancellationRequested);
        }
    }
    private sealed class InterceptedReservations(IReservationService inner, string mode, CancellationTokenSource disconnect) : IReservationService
    {
        public List<CancellationToken> CreateTokens { get; } = [];
        public int Cancellations { get; private set; }
        public async Task<ReservationResult> CreateAsync(ReservationActor actor, CreateReservationCommand command, CancellationToken cancellationToken = default)
        {
            CreateTokens.Add(cancellationToken);
            if (mode == "retry" && CreateTokens.Count == 1) throw new ReservationWorkflowException(ReservationErrorCode.ScheduleConflict, "Candidate taken.");
            if (mode == "forbidden" && CreateTokens.Count == 2) throw new ReservationWorkflowException(ReservationErrorCode.Forbidden, "Access changed.");
            var result = await inner.CreateAsync(actor, command, cancellationToken);
            if (mode == "disconnect" && CreateTokens.Count == 1) disconnect.Cancel();
            return result;
        }
        public Task<ReservationResult> ApproveAsync(ReservationActor actor, Guid reservationId, CancellationToken cancellationToken = default) => inner.ApproveAsync(actor, reservationId, cancellationToken);
        public Task<ReservationResult> RejectAsync(ReservationActor actor, Guid reservationId, string reason, CancellationToken cancellationToken = default) => inner.RejectAsync(actor, reservationId, reason, cancellationToken);
        public Task<ReservationResult> CancelAsync(ReservationActor actor, Guid reservationId, string? reason = null, CancellationToken cancellationToken = default)
        { Cancellations++; Assert.False(cancellationToken.CanBeCanceled); return inner.CancelAsync(actor, reservationId, reason, cancellationToken); }
        public Task<ReservationResult> ReleaseAsync(ReservationActor actor, Guid reservationId, EquipmentCondition conditionAtRelease, string? notes = null, CancellationToken cancellationToken = default) => inner.ReleaseAsync(actor, reservationId, conditionAtRelease, notes, cancellationToken);
        public Task<ReservationResult> ReturnAsync(ReservationActor actor, Guid reservationId, EquipmentCondition conditionAtReturn, string? notes = null, CancellationToken cancellationToken = default) => inner.ReturnAsync(actor, reservationId, conditionAtReturn, notes, cancellationToken);
        public Task<int> ExpireDueAsync(int batchSize = 100, CancellationToken cancellationToken = default) => inner.ExpireDueAsync(batchSize, cancellationToken);
    }
}
