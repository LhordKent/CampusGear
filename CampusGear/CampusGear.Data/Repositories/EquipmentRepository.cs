using CampusGear.Data.Interfaces;
using CampusGear.Data.Models;
using CampusGear.Data.QueryModels;
using CampusGear.Resources.Constants;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Data.Repositories
{
    public sealed class EquipmentRepository : BaseRepository, IEquipmentRepository
    {
        public EquipmentRepository(IUnitOfWork unitOfWork, ApplicationDbContext db)
            : base(unitOfWork, db) { }

        public Task<EquipmentItem?> FindAsync(Guid id, CancellationToken ct)
        {
            return Context.EquipmentItems.SingleOrDefaultAsync(x => x.Id == id, ct);
        }

        public async Task<bool> HasHistoryAsync(Guid id, CancellationToken ct)
        {
            return await Context.Reservations.AnyAsync(x => x.EquipmentItemId == id, ct)
                || await Context.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == id, ct);
        }

        public Task<bool> HasOutstandingLoanAsync(Guid id, CancellationToken ct)
        {
            return Context.Loans.AnyAsync(x => x.Reservation.EquipmentItemId == id && x.ReturnedAtUtc == null, ct);
        }

        public Task<bool> HasLiveReservationsAsync(Guid id, DateTime now, CancellationToken ct)
        {
            return Context.Reservations.AnyAsync(
                x =>
                    x.EquipmentItemId == id
                    && (x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now),
                ct
            );
        }

        public async Task<InventoryData> InventoryAsync(string? search, int page, CancellationToken ct)
        {
            var query = Context.EquipmentItems.AsNoTracking();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x => x.Name.Contains(search) || x.AssetCode.Contains(search));
            }

            var total = await query.CountAsync(ct);
            var pages = Math.Max(1, (int)Math.Ceiling(total / 15d));
            page = Math.Clamp(page, 1, pages);
            var items = await query
                .Include(x => x.Category)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenBy(x => x.AssetCode)
                .Skip((page - 1) * 15)
                .Take(15)
                .ToListAsync(ct);
            var categories = await Context
                .EquipmentCategories.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new CategoryRecord(x.Id, x.Name, x.Description, x.IsActive))
                .ToListAsync(ct);
            return new(items.Select(EquipmentRecord.From).ToList(), categories, page, pages, total);
        }

        public void Add(EquipmentItem item) => Context.EquipmentItems.Add(item);

        public void Remove(EquipmentItem item) => Context.EquipmentItems.Remove(item);

        public void MarkUpdated(EquipmentItem item)
        {
            Context.Entry(item).Property(x => x.UpdatedAtUtc).IsModified = true;
        }
    }
}
