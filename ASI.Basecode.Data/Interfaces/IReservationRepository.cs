using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public sealed record ExpiryCandidate(Guid Id, Guid EquipmentItemId);

    public sealed record BookingTime(DateTime StartAtUtc, DateTime EndAtUtc);

    public interface IReservationRepository
    {
        Task<Reservation?> FindByRequestIdAsync(Guid requestId, CancellationToken ct);
        Task<Reservation?> FindAsync(Guid id, CancellationToken ct);
        Task<Reservation?> FindWorkflowAsync(Guid id, CancellationToken ct);
        Task<Guid?> GetItemIdAsync(Guid id, CancellationToken ct);
        Task<bool> HasConflictAsync(Guid itemId, DateTime start, DateTime end, DateTime now, Guid? excluding, CancellationToken ct);
        Task<IReadOnlyList<ExpiryCandidate>> ExpiryCandidatesAsync(DateTime now, int size, CancellationToken ct);

        /// <summary>
        /// The distinct booking times (start, end) of a borrower's active reservations: pending with a live hold,
        /// approved and not yet ended, or released and not yet returned. Units booked together share one time.
        /// </summary>
        Task<IReadOnlyList<BookingTime>> ActiveBookingTimesAsync(Guid borrowerProfileId, DateTime now, CancellationToken ct);

        void Add(Reservation reservation);
    }
}
