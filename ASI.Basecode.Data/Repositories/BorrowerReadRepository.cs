using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public sealed class BorrowerReadRepository : BaseRepository, IBorrowerReadRepository
    {
        public BorrowerReadRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        public Task<BorrowerProfile?> FindProfileAsync(string? userId, CancellationToken ct)
        {
            return Context.BorrowerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        }

        public async Task<IReadOnlyList<CatalogUnit>> CatalogAsync(CancellationToken ct)
        {
            return await Context
                .EquipmentItems.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.AssetCode)
                .Select(x => new CatalogUnit(
                    x.Id,
                    x.CategoryId,
                    x.Category.Name,
                    x.Name,
                    x.Description,
                    x.ImagePath,
                    x.Location,
                    x.Condition,
                    x.CreatedAtUtc,
                    !x.IsMaintenanceHold && x.Condition != EquipmentCondition.Damaged && x.Condition != EquipmentCondition.Unserviceable,
                    x.AssetCode
                ))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Guid>> InUseAsync(DateTime now, CancellationToken ct)
        {
            return await Context
                .Reservations.AsNoTracking()
                .Where(x =>
                    x.Status == ReservationStatus.Released
                    || x.Status == ReservationStatus.Approved && x.StartAtUtc <= now && x.EndAtUtc > now
                )
                .Select(x => x.EquipmentItemId)
                .Distinct()
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<RequestRecord>> FindRequestsAsync(Guid profileId, Guid[] ids, CancellationToken ct)
        {
            return await Context
                .Reservations.AsNoTracking()
                .Where(x => x.BorrowerProfileId == profileId && ids.Contains(x.RequestId))
                .Select(x => new RequestRecord(x.Id, x.Status))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Reservation>> SubmittedAsync(Guid profileId, IReadOnlyList<Guid> ids, CancellationToken ct)
        {
            return await Context
                .Reservations.AsNoTracking()
                .Include(x => x.EquipmentItem)
                .Where(x => ids.Contains(x.Id) && x.BorrowerProfileId == profileId)
                .OrderBy(x => x.EquipmentItem.AssetCode)
                .ToListAsync(ct);
        }

        public async Task<BorrowerHomeData> HomeAsync(Guid profileId, DateTime now, CancellationToken ct)
        {
            var mine = Context.Reservations.AsNoTracking().Where(x => x.BorrowerProfileId == profileId);
            var upcoming = await mine.Include(x => x.EquipmentItem)
                .Include(x => x.Loan)
                .Where(x =>
                    x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now
                    || x.Status == ReservationStatus.Approved
                    || x.Status == ReservationStatus.Released
                )
                .OrderBy(x => x.StartAtUtc)
                .Take(4)
                .ToListAsync(ct);
            return new(
                upcoming,
                await mine.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now, ct),
                await mine.CountAsync(x => x.Status == ReservationStatus.Approved, ct),
                await Context.Loans.CountAsync(x => x.Reservation.BorrowerProfileId == profileId && x.ReturnedAtUtc == null, ct),
                await Context.Loans.CountAsync(
                    x => x.Reservation.BorrowerProfileId == profileId && x.ReturnedAtUtc == null && x.DueAtUtc < now,
                    ct
                ),
                await mine.CountAsync(x => x.Status == ReservationStatus.Completed, ct)
            );
        }

        public async Task<ReservationRows> HistoryAsync(
            Guid profileId,
            string status,
            string? search,
            DateTime? fromUtc,
            DateTime? toUtc,
            DateTime now,
            int page,
            CancellationToken ct
        )
        {
            var query = Context.Reservations.AsNoTracking().Where(x => x.BorrowerProfileId == profileId);
            query = status switch
            {
                "pending" => query.Where(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now),
                "approved" => query.Where(x => x.Status == ReservationStatus.Approved),
                "borrowed" => query.Where(x => x.Status == ReservationStatus.Released),
                "returned" => query.Where(x => x.Status == ReservationStatus.Completed),
                "late" => query.Where(x =>
                    x.Loan != null && (x.Loan.ReturnedAtUtc == null && x.Loan.DueAtUtc < now || x.Loan.ReturnedAtUtc > x.Loan.DueAtUtc)
                ),
                "cancelled" => query.Where(x => x.Status == ReservationStatus.Cancelled),
                _ => query,
            };
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x =>
                    x.EquipmentItem.Name.Contains(search) || x.EquipmentItem.AssetCode.Contains(search) || x.Purpose.Contains(search)
                );
            }

            if (fromUtc is not null)
            {
                query = query.Where(x => x.StartAtUtc >= fromUtc);
            }

            if (toUtc is not null)
            {
                query = query.Where(x => x.StartAtUtc < toUtc);
            }

            var total = await query.CountAsync(ct);
            var pages = Math.Max(1, (int)Math.Ceiling(total / 12d));
            page = Math.Clamp(page, 1, pages);
            return new(
                await query
                    .Include(x => x.EquipmentItem)
                    .Include(x => x.Loan)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Skip((page - 1) * 12)
                    .Take(12)
                    .ToListAsync(ct),
                page,
                pages,
                total
            );
        }
    }
}
