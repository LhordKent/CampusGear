using CampusGear.Data.Interfaces;
using CampusGear.Data.QueryModels;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class BorrowerReadRepository(ApplicationDbContext db) : IBorrowerReadRepository
{
    public Task<BorrowerProfile?> FindProfileAsync(string? userId, CancellationToken ct) => db.BorrowerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
    public async Task<IReadOnlyList<CatalogUnit>> CatalogAsync(CancellationToken ct) => await db.EquipmentItems.AsNoTracking().Where(x => x.IsActive)
        .OrderBy(x => x.Name).ThenBy(x => x.AssetCode).Select(x => new CatalogUnit(x.Id, x.CategoryId, x.Category.Name, x.Name, x.Description,
            x.ImagePath, x.Location, x.Condition, x.CreatedAtUtc, !x.IsMaintenanceHold && x.Condition != EquipmentCondition.Damaged &&
            x.Condition != EquipmentCondition.Unserviceable, x.AssetCode)).ToListAsync(ct);
    public async Task<IReadOnlyList<Guid>> InUseAsync(DateTime now, CancellationToken ct) => await db.Reservations.AsNoTracking()
        .Where(x => x.Status == ReservationStatus.Released || x.Status == ReservationStatus.Approved && x.StartAtUtc <= now && x.EndAtUtc > now)
        .Select(x => x.EquipmentItemId).Distinct().ToListAsync(ct);
    public async Task<IReadOnlyList<Guid>> BlockedAsync(IReadOnlyList<Guid> ids, DateTime start, DateTime end, DateTime now, CancellationToken ct) =>
        await db.Reservations.AsNoTracking().Where(x => ids.Contains(x.EquipmentItemId) && x.StartAtUtc < end && x.EndAtUtc > start &&
            (x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Released || x.Status == ReservationStatus.Pending &&
            x.HoldExpiresAtUtc != null && x.HoldExpiresAtUtc > now && x.StartAtUtc > now)).Select(x => x.EquipmentItemId).Distinct().ToListAsync(ct);
    public async Task<IReadOnlyList<RequestRecord>> FindRequestsAsync(Guid profileId, Guid[] ids, CancellationToken ct) => await db.Reservations.AsNoTracking()
        .Where(x => x.BorrowerProfileId == profileId && ids.Contains(x.RequestId)).Select(x => new RequestRecord(x.Id, x.Status)).ToListAsync(ct);
    public async Task<IReadOnlyList<Reservation>> SubmittedAsync(Guid profileId, IReadOnlyList<Guid> ids, CancellationToken ct) => await db.Reservations.AsNoTracking()
        .Include(x => x.EquipmentItem).Where(x => ids.Contains(x.Id) && x.BorrowerProfileId == profileId).OrderBy(x => x.EquipmentItem.AssetCode).ToListAsync(ct);
    public async Task<BorrowerHomeData> HomeAsync(Guid profileId, DateTime now, CancellationToken ct)
    {
        var mine = db.Reservations.AsNoTracking().Where(x => x.BorrowerProfileId == profileId);
        var upcoming = await mine.Include(x => x.EquipmentItem).Include(x => x.Loan)
            .Where(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now ||
                x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Released)
            .OrderBy(x => x.StartAtUtc).Take(4).ToListAsync(ct);
        return new(upcoming, await mine.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now, ct),
            await mine.CountAsync(x => x.Status == ReservationStatus.Approved, ct),
            await db.Loans.CountAsync(x => x.Reservation.BorrowerProfileId == profileId && x.ReturnedAtUtc == null, ct),
            await db.Loans.CountAsync(x => x.Reservation.BorrowerProfileId == profileId && x.ReturnedAtUtc == null && x.DueAtUtc < now, ct),
            await mine.CountAsync(x => x.Status == ReservationStatus.Completed, ct));
    }
    public async Task<ReservationRows> HistoryAsync(Guid profileId, string status, string? search, DateTime? fromUtc, DateTime? toUtc, DateTime now, int page, CancellationToken ct)
    {
        var query = db.Reservations.AsNoTracking().Where(x => x.BorrowerProfileId == profileId);
        query = status switch
        {
            "pending" => query.Where(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now),
            "approved" => query.Where(x => x.Status == ReservationStatus.Approved),
            "borrowed" => query.Where(x => x.Status == ReservationStatus.Released),
            "returned" => query.Where(x => x.Status == ReservationStatus.Completed),
            "late" => query.Where(x => x.Loan != null && (x.Loan.ReturnedAtUtc == null && x.Loan.DueAtUtc < now || x.Loan.ReturnedAtUtc > x.Loan.DueAtUtc)),
            "cancelled" => query.Where(x => x.Status == ReservationStatus.Cancelled),
            _ => query
        };
        if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.EquipmentItem.Name.Contains(search) || x.EquipmentItem.AssetCode.Contains(search) || x.Purpose.Contains(search));
        if (fromUtc is not null) query = query.Where(x => x.StartAtUtc >= fromUtc);
        if (toUtc is not null) query = query.Where(x => x.StartAtUtc < toUtc);
        var total = await query.CountAsync(ct); var pages = Math.Max(1, (int)Math.Ceiling(total / 12d)); page = Math.Clamp(page, 1, pages);
        return new(await query.Include(x => x.EquipmentItem).Include(x => x.Loan).OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * 12).Take(12).ToListAsync(ct), page, pages, total);
    }
    public async Task<IReadOnlyList<CalendarItemData>> CalendarItemsAsync(CancellationToken ct) => await db.EquipmentItems.AsNoTracking().Where(x => x.IsActive)
        .OrderBy(x => x.Name).ThenBy(x => x.AssetCode).Select(x => new CalendarItemData(x.Id, x.Name, x.AssetCode,
            x.IsMaintenanceHold || x.Condition == EquipmentCondition.Damaged || x.Condition == EquipmentCondition.Unserviceable, x.CategoryId, x.Category.Name, x.ImagePath)).ToListAsync(ct);
    public async Task<IReadOnlyList<Reservation>> CalendarBookingsAsync(Guid itemId, DateTime fromUtc, DateTime toUtc, DateTime now, bool staff, CancellationToken ct)
    {
        var query = db.Reservations.AsNoTracking().Include(x => x.BorrowerProfile).ThenInclude(x => x.User)
            .Where(x => x.EquipmentItemId == itemId && x.StartAtUtc < toUtc && x.EndAtUtc > fromUtc &&
                (x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Released || x.Status == ReservationStatus.Completed ||
                x.Status == ReservationStatus.Pending && (!staff || x.HoldExpiresAtUtc > now && x.StartAtUtc > now)));
        return staff ? await query.OrderBy(x => x.StartAtUtc).ToListAsync(ct) : await query.ToListAsync(ct);
    }
    public async Task<IReadOnlyList<EquipmentStateData>> EquipmentStatesAsync(DateTime now, CancellationToken ct) => await db.EquipmentItems.AsNoTracking()
        .Select(x => new EquipmentStateData(x.Id, x.IsActive, x.IsMaintenanceHold || x.Condition == EquipmentCondition.Damaged || x.Condition == EquipmentCondition.Unserviceable,
            db.Loans.Any(l => l.Reservation.EquipmentItemId == x.Id && l.ReturnedAtUtc == null),
            db.Reservations.Any(r => r.EquipmentItemId == x.Id && r.Status == ReservationStatus.Approved && r.StartAtUtc <= now && r.EndAtUtc > now))).ToListAsync(ct);
}
