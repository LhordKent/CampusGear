using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public sealed class EquipmentAvailabilityRepository : BaseRepository, IEquipmentAvailabilityRepository
    {
        public EquipmentAvailabilityRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext context)
            : base(unitOfWork, context) { }

        public async Task<IReadOnlyList<Guid>> BlockedAsync(
            IReadOnlyList<Guid> ids,
            DateTime start,
            DateTime end,
            DateTime now,
            CancellationToken ct
        )
        {
            return await Blocking(ids, start, end, now).Select(x => x.EquipmentItemId).Distinct().ToListAsync(ct);
        }

        public async Task<IReadOnlyList<BusyIntervalData>> BusyIntervalsAsync(
            IReadOnlyList<Guid> ids,
            DateTime fromUtc,
            DateTime toUtc,
            DateTime now,
            CancellationToken ct
        )
        {
            return await Blocking(ids, fromUtc, toUtc, now)
                .OrderBy(x => x.StartAtUtc)
                .Select(x => new BusyIntervalData(x.EquipmentItemId, x.StartAtUtc, x.EndAtUtc))
                .ToListAsync(ct);
        }

        // The one rule for "this unit is taken": the booking check and the borrower's date picker both use it.
        private IQueryable<Reservation> Blocking(IReadOnlyList<Guid> ids, DateTime start, DateTime end, DateTime now)
        {
            return Context
                .Reservations.AsNoTracking()
                .Where(x =>
                    ids.Contains(x.EquipmentItemId)
                    && x.StartAtUtc < end
                    && x.EndAtUtc > start
                    && (
                        x.Status == ReservationStatus.Approved
                        || x.Status == ReservationStatus.Released
                        || x.Status == ReservationStatus.Pending
                            && x.HoldExpiresAtUtc != null
                            && x.HoldExpiresAtUtc > now
                            && x.StartAtUtc > now
                    )
                );
        }

        public async Task<IReadOnlyList<CalendarItemData>> CalendarItemsAsync(CancellationToken ct)
        {
            return await Context
                .EquipmentItems.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.AssetCode)
                .Select(x => new CalendarItemData(
                    x.Id,
                    x.Name,
                    x.AssetCode,
                    x.IsMaintenanceHold || x.Condition == EquipmentCondition.Damaged || x.Condition == EquipmentCondition.Unserviceable,
                    x.CategoryId,
                    x.Category.Name,
                    x.ImagePath
                ))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Reservation>> CalendarBookingsAsync(
            Guid itemId,
            DateTime fromUtc,
            DateTime toUtc,
            DateTime now,
            bool staff,
            CancellationToken ct
        )
        {
            // The loan is loaded so an early return frees the rest of its booking on the calendar.
            var query = Context
                .Reservations.AsNoTracking()
                .Include(x => x.BorrowerProfile)
                .ThenInclude(x => x.User)
                .Include(x => x.Loan)
                .Where(x =>
                    x.EquipmentItemId == itemId
                    && x.StartAtUtc < toUtc
                    && x.EndAtUtc > fromUtc
                    && (
                        x.Status == ReservationStatus.Approved
                        || x.Status == ReservationStatus.Released
                        || x.Status == ReservationStatus.Completed
                        || x.Status == ReservationStatus.Pending && (!staff || x.HoldExpiresAtUtc > now && x.StartAtUtc > now)
                    )
                );
            return staff ? await query.OrderBy(x => x.StartAtUtc).ToListAsync(ct) : await query.ToListAsync(ct);
        }

        public async Task<IReadOnlyList<EquipmentStateData>> EquipmentStatesAsync(DateTime now, CancellationToken ct)
        {
            return await Context
                .EquipmentItems.AsNoTracking()
                .Select(x => new EquipmentStateData(
                    x.Id,
                    x.IsActive,
                    x.IsMaintenanceHold || x.Condition == EquipmentCondition.Damaged || x.Condition == EquipmentCondition.Unserviceable,
                    Context.Loans.Any(l => l.Reservation.EquipmentItemId == x.Id && l.ReturnedAtUtc == null),
                    Context.Reservations.Any(r =>
                        r.EquipmentItemId == x.Id && r.Status == ReservationStatus.Approved && r.StartAtUtc <= now && r.EndAtUtc > now
                    )
                ))
                .ToListAsync(ct);
        }
    }
}
