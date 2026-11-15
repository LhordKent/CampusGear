using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IReservationService
    {
        Task<ReservationResult> CreateAsync(
            ReservationActor actor,
            CreateReservationCommand command,
            CancellationToken cancellationToken = default
        );
        Task<ReservationResult> ApproveAsync(ReservationActor actor, Guid reservationId, CancellationToken cancellationToken = default);
        Task<ReservationResult> RejectAsync(
            ReservationActor actor,
            Guid reservationId,
            string reason,
            CancellationToken cancellationToken = default
        );
        Task<ReservationResult> CancelAsync(
            ReservationActor actor,
            Guid reservationId,
            string? reason = null,
            CancellationToken cancellationToken = default
        );
        Task<ReservationResult> ReleaseAsync(
            ReservationActor actor,
            Guid reservationId,
            EquipmentCondition conditionAtRelease,
            string? notes = null,
            CancellationToken cancellationToken = default
        );
        Task<ReservationResult> ReturnAsync(
            ReservationActor actor,
            Guid reservationId,
            EquipmentCondition conditionAtReturn,
            string? notes = null,
            CancellationToken cancellationToken = default
        );
        Task<int> ExpireDueAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    }
}
