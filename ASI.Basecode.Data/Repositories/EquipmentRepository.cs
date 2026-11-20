using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;

// =====================================================================
// EQUIPMENT REPOSITORY  -  database queries for equipment items (used by InventoryService and others).
// =====================================================================
namespace ASI.Basecode.Data.Repositories
{
    public sealed class EquipmentRepository : BaseRepository, IEquipmentRepository
    {
        public EquipmentRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        public Task<EquipmentItem?> FindAsync(Guid id, CancellationToken ct)
        {
            return Context.EquipmentItems.SingleOrDefaultAsync(x => x.Id == id, ct);
        }

        // Has this item ever been booked or repaired? (If yes, delete only makes it inactive.)
        public async Task<bool> HasHistoryAsync(Guid id, CancellationToken ct)
        {
            return await Context.Reservations.AnyAsync(x => x.EquipmentItemId == id, ct)
                || await Context.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == id, ct);
        }

        // Is the item out on loan right now?
        public Task<bool> HasOutstandingLoanAsync(Guid id, CancellationToken ct)
        {
            return Context.Loans.AnyAsync(x => x.Reservation.EquipmentItemId == id && x.ReturnedAtUtc == null, ct);
        }

        // Does the item have Approved bookings or live Pending requests?
        public Task<bool> HasLiveReservationsAsync(Guid id, DateTime now, CancellationToken ct)
        {
            return Context.Reservations.AnyAsync(
                x =>
                    x.EquipmentItemId == id
                    && (x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now),
                ct
            );
        }

        // The Equipment page: items (15 per page, newest first), categories, and known storage locations.
        public async Task<InventoryData> InventoryAsync(string? search, int page, CancellationToken ct)
        {
            var query = Context.EquipmentItems.AsNoTracking();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x => x.Name.Contains(search) || x.AssetCode.Contains(search) || x.Category.Name.Contains(search));
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
                .Select(x => new CategoryRecord(x.Id, x.Name, x.Description, x.IsActive, x.AssetCodeSeries!.Prefix))
                .ToListAsync(ct);
            var locations = await Context
                .EquipmentItems.AsNoTracking()
                .Where(x => x.Location != null && x.Location != "")
                .Select(x => x.Location!)
                .Distinct()
                .OrderBy(x => x)
                .Take(200)
                .ToListAsync(ct);
            return new(items.Select(EquipmentRecord.From).ToList(), categories, page, pages, total, locations);
        }

        // For the Edit modal: open loan due date, and number of upcoming bookings.
        public async Task<EquipmentEditState> EditStateAsync(Guid id, DateTime now, CancellationToken ct)
        {
            var due = await Context
                .Loans.AsNoTracking()
                .Where(x => x.Reservation.EquipmentItemId == id && x.ReturnedAtUtc == null)
                .Select(x => (DateTime?)x.DueAtUtc)
                .FirstOrDefaultAsync(ct);
            // Same rule as HasLiveReservationsAsync, which blocks making the item inactive.
            var upcoming = await Context.Reservations.CountAsync(
                x =>
                    x.EquipmentItemId == id
                    && (x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now),
                ct
            );
            // datetime2 comes back with Kind=Unspecified; the column contract is UTC.
            return new(due is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : null, upcoming);
        }

        public void Add(EquipmentItem item) => Context.EquipmentItems.Add(item);

        public void Remove(EquipmentItem item) => Context.EquipmentItems.Remove(item);

        // Marks the item as changed so its row version moves on (detects edits made at the same time).
        public void MarkUpdated(EquipmentItem item)
        {
            Context.Entry(item).Property(x => x.UpdatedAtUtc).IsModified = true;
        }
    }
}
