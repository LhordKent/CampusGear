using CampusGear.Data.Interfaces;
using CampusGear.Models;
using CampusGear.Data.QueryModels;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class MaintenanceRepository(ApplicationDbContext db) : IMaintenanceRepository
{
    public Task<bool> HasOpenAsync(Guid itemId, CancellationToken ct, Guid? excludingCase = null) =>
        db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == itemId && x.Id != excludingCase && x.Status != MaintenanceStatus.Closed, ct);
    public void Add(MaintenanceCase item) => db.MaintenanceCases.Add(item);
    public Task<Guid?> GetItemIdAsync(Guid id, CancellationToken ct) => db.MaintenanceCases.AsNoTracking()
        .Where(x => x.Id == id).Select(x => (Guid?)x.EquipmentItemId).SingleOrDefaultAsync(ct);
    public Task<MaintenanceCase> FindWithItemAsync(Guid id, CancellationToken ct) =>
        db.MaintenanceCases.Include(x => x.EquipmentItem).SingleAsync(x => x.Id == id, ct);
    public async Task<MaintenanceListData> ListAsync(string status, string? search, int page, CancellationToken ct)
    {
        var all = db.MaintenanceCases.AsNoTracking();
        var open = await all.CountAsync(x => x.Status != MaintenanceStatus.Closed, ct);
        var closed = await all.CountAsync(x => x.Status == MaintenanceStatus.Closed, ct);
        var query = status == "closed" ? all.Where(x => x.Status == MaintenanceStatus.Closed) : all.Where(x => x.Status != MaintenanceStatus.Closed);
        if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.EquipmentItem.Name.Contains(search) || x.EquipmentItem.AssetCode.Contains(search) ||
            x.Description.Contains(search) || x.Resolution != null && x.Resolution.Contains(search));
        var total = await query.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / 15d));
        page = Math.Clamp(page, 1, pages);
        var ordered = status == "closed" ? query.OrderByDescending(x => x.ClosedAtUtc).ThenByDescending(x => x.OpenedAtUtc) : query.OrderByDescending(x => x.OpenedAtUtc);
        var rows = await ordered.Include(x => x.EquipmentItem).Include(x => x.OpenedByUser).Include(x => x.ClosedByUser)
            .Skip((page - 1) * 15).Take(15).ToListAsync(ct);
        var cases = rows.Select(x => new MaintenanceRecord(x.Id, x.EquipmentItemId, x.Status, x.Description, x.Resolution,
            x.OpenedAtUtc, x.ClosedAtUtc, x.RowVersion.ToArray(), EquipmentRecord.From(x.EquipmentItem), new(x.OpenedByUser.FullName),
            x.ClosedByUser is null ? null : new(x.ClosedByUser.FullName))).ToList();
        var choices = await db.EquipmentItems.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ThenBy(x => x.AssetCode)
            .Select(x => new MaintenanceChoice(x.Id, x.Name, x.AssetCode, x.Condition,
                db.MaintenanceCases.Any(c => c.EquipmentItemId == x.Id && c.Status != MaintenanceStatus.Closed),
                db.Loans.Any(l => l.Reservation.EquipmentItemId == x.Id && l.ReturnedAtUtc == null))).ToListAsync(ct);
        return new(cases, choices, page, pages, total, open, closed);
    }
}
