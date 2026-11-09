using CampusGear.Data.Interfaces;
using CampusGear.Data.QueryModels;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class CustodianReadRepository(ApplicationDbContext db) : ICustodianReadRepository
{
    public Task<Reservation?> DetailsAsync(Guid id, CancellationToken ct) => db.Reservations.AsNoTracking()
        .Include(x => x.BorrowerProfile).ThenInclude(x => x.User).Include(x => x.EquipmentItem).ThenInclude(x => x.Category)
        .Include(x => x.DecidedByUser).Include(x => x.Loan).ThenInclude(x => x!.ReleasedByUser)
        .Include(x => x.Loan).ThenInclude(x => x!.ReceivedByUser).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<CustodianRows> ListAsync(string queue, string status, string? search, DateTime? from, DateTime? to, DateTime now, int page, CancellationToken ct)
    {
        var query = db.Reservations.AsNoTracking();
        if (queue == "approvals")
        {
            query = query.Where(x => x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now);
            if (status == "pending") query = query.Where(x => x.Status == ReservationStatus.Pending);
            if (status == "approved") query = query.Where(x => x.Status == ReservationStatus.Approved);
        }
        else if (queue == "release") query = query.Where(x => x.Status == ReservationStatus.Approved);
        else if (queue == "active") query = query.Where(x => x.Status == ReservationStatus.Released && x.Loan != null && x.Loan.ReturnedAtUtc == null);
        else query = status switch
        {
            "pending" => query.Where(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now),
            "approved" => query.Where(x => x.Status == ReservationStatus.Approved),
            "borrowed" => query.Where(x => x.Status == ReservationStatus.Released),
            "returned" => query.Where(x => x.Status == ReservationStatus.Completed),
            "rejected" => query.Where(x => x.Status == ReservationStatus.Rejected),
            "cancelled" => query.Where(x => x.Status == ReservationStatus.Cancelled),
            "expired" => query.Where(x => x.Status == ReservationStatus.Expired || x.Status == ReservationStatus.Pending && (x.HoldExpiresAtUtc == null || x.HoldExpiresAtUtc <= now || x.StartAtUtc <= now)),
            "late" => query.Where(x => x.Loan != null && (x.Loan.ReturnedAtUtc == null && x.Loan.DueAtUtc < now || x.Loan.ReturnedAtUtc > x.Loan.DueAtUtc)),
            _ => query
        };
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.EquipmentItem.Name.Contains(search) || x.EquipmentItem.AssetCode.Contains(search) ||
            x.BorrowerProfile.User.FullName.Contains(search) || x.BorrowerProfile.User.Email!.Contains(search));
        if (from is not null) query = query.Where(x => x.StartAtUtc >= from);
        if (to is not null) query = query.Where(x => x.StartAtUtc < to);
        var total = await query.CountAsync(ct); var pages = Math.Max(1, (int)Math.Ceiling(total / 12d)); page = Math.Clamp(page, 1, pages);
        var included = query.Include(x => x.BorrowerProfile).ThenInclude(x => x.User).Include(x => x.EquipmentItem).Include(x => x.Loan);
        var ordered = queue switch
        {
            "active" => included.OrderByDescending(x => x.Loan!.ReleasedAtUtc).ThenByDescending(x => x.CreatedAtUtc),
            "release" => included.OrderByDescending(x => x.DecidedAtUtc).ThenByDescending(x => x.CreatedAtUtc),
            _ => included.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
        };
        return new(await ordered.Skip((page - 1) * 12).Take(12).ToListAsync(ct), page, pages, total,
            await db.Reservations.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now, ct),
            await db.Reservations.CountAsync(x => x.Status == ReservationStatus.Approved, ct), await db.Loans.CountAsync(x => x.ReturnedAtUtc == null, ct));
    }
}
