using CampusGear.Data.Interfaces;
using CampusGear.Data.QueryModels;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class EquipmentRepository(ApplicationDbContext db) : IEquipmentRepository
{
    public Task<EquipmentItem?> FindAsync(Guid id, CancellationToken ct) => db.EquipmentItems.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<bool> HasHistoryAsync(Guid id, CancellationToken ct) =>
        await db.Reservations.AnyAsync(x => x.EquipmentItemId == id, ct) || await db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == id, ct);
    public Task<bool> HasOutstandingLoanAsync(Guid id, CancellationToken ct) =>
        db.Loans.AnyAsync(x => x.Reservation.EquipmentItemId == id && x.ReturnedAtUtc == null, ct);
    public Task<bool> HasLiveReservationsAsync(Guid id, DateTime now, CancellationToken ct) =>
        db.Reservations.AnyAsync(x => x.EquipmentItemId == id && (x.Status == ReservationStatus.Approved ||
            x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now), ct);
    public async Task<InventoryData> InventoryAsync(string? search, int page, CancellationToken ct)
    {
        var query = db.EquipmentItems.AsNoTracking();
        if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.Name.Contains(search) || x.AssetCode.Contains(search));
        var total = await query.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / 15d));
        page = Math.Clamp(page, 1, pages);
        var items = await query.Include(x => x.Category).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.AssetCode)
            .Skip((page - 1) * 15).Take(15).ToListAsync(ct);
        var categories = await db.EquipmentCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new CategoryRecord(x.Id, x.Name, x.Description, x.IsActive)).ToListAsync(ct);
        return new(items.Select(EquipmentRecord.From).ToList(), categories, page, pages, total);
    }
    public void Add(EquipmentItem item) => db.EquipmentItems.Add(item);
    public void Remove(EquipmentItem item) => db.EquipmentItems.Remove(item);
    public void MarkUpdated(EquipmentItem item) => db.Entry(item).Property(x => x.UpdatedAtUtc).IsModified = true;
}
