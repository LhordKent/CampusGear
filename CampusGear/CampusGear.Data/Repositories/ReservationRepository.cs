using CampusGear.Data.Interfaces;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class ReservationRepository(ApplicationDbContext db) : IReservationRepository
{
    public Task<Reservation?> FindByRequestIdAsync(Guid requestId, CancellationToken ct) => db.Reservations.AsNoTracking()
        .Include(x => x.BorrowerProfile).Include(x => x.Loan).SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
    public Task<Reservation?> FindAsync(Guid id, CancellationToken ct) => db.Reservations.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Reservation?> FindWorkflowAsync(Guid id, CancellationToken ct) => db.Reservations.Include(x => x.Loan)
        .Include(x => x.EquipmentItem).Include(x => x.BorrowerProfile).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Guid?> GetItemIdAsync(Guid id, CancellationToken ct) => db.Reservations.AsNoTracking()
        .Where(x => x.Id == id).Select(x => (Guid?)x.EquipmentItemId).SingleOrDefaultAsync(ct);
    public Task<bool> HasConflictAsync(Guid itemId, DateTime start, DateTime end, DateTime now, Guid? excluding, CancellationToken ct) =>
        db.Reservations.AnyAsync(x => x.EquipmentItemId == itemId && (excluding == null || x.Id != excluding) &&
            x.StartAtUtc < end && x.EndAtUtc > start && (x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Released ||
            x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc != null && x.HoldExpiresAtUtc > now && x.StartAtUtc > now), ct);
    public async Task<IReadOnlyList<ExpiryCandidate>> ExpiryCandidatesAsync(DateTime now, int size, CancellationToken ct) =>
        await db.Reservations.AsNoTracking().Where(x => x.Status == ReservationStatus.Pending &&
            (x.HoldExpiresAtUtc == null || x.HoldExpiresAtUtc <= now || x.StartAtUtc <= now))
            .OrderBy(x => x.HoldExpiresAtUtc).Select(x => new ExpiryCandidate(x.Id, x.EquipmentItemId)).Take(size).ToListAsync(ct);
    public void Add(Reservation reservation) => db.Reservations.Add(reservation);
}
