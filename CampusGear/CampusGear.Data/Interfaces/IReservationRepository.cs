using CampusGear.Models;
namespace CampusGear.Data.Interfaces;
public sealed record ExpiryCandidate(Guid Id, Guid EquipmentItemId);
public interface IReservationRepository
{
    Task<Reservation?> FindByRequestIdAsync(Guid requestId, CancellationToken ct);
    Task<Reservation?> FindAsync(Guid id, CancellationToken ct);
    Task<Reservation?> FindWorkflowAsync(Guid id, CancellationToken ct);
    Task<Guid?> GetItemIdAsync(Guid id, CancellationToken ct);
    Task<bool> HasConflictAsync(Guid itemId, DateTime start, DateTime end, DateTime now, Guid? excluding, CancellationToken ct);
    Task<IReadOnlyList<ExpiryCandidate>> ExpiryCandidatesAsync(DateTime now, int size, CancellationToken ct);
    void Add(Reservation reservation);
}
