using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public sealed class DashboardReadRepository : BaseRepository, IDashboardReadRepository
    {
        public DashboardReadRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        public async Task<DeskData> DeskAsync(DateTime now, DateTime end, CancellationToken ct)
        {
            var pending = await Context.Reservations.CountAsync(
                reservation =>
                    reservation.Status == ReservationStatus.Pending && reservation.HoldExpiresAtUtc > now && reservation.StartAtUtc > now,
                ct
            );
            // Ready once pickup opens (ReleaseWindow.EarlyPickup before the start), until the booking ends.
            var pickupOpensBy = now + ReleaseWindow.EarlyPickup;
            var readyToRelease = await Context.Reservations.CountAsync(
                reservation =>
                    reservation.Status == ReservationStatus.Approved && reservation.StartAtUtc <= pickupOpensBy && reservation.EndAtUtc > now,
                ct
            );
            // A loan inside its ReturnGrace period after the due time still counts as due, not overdue.
            var overdueIfDueBefore = ReturnGrace.OverdueIfDueBefore(now);
            var overdue = await Context.Loans.CountAsync(loan => loan.ReturnedAtUtc == null && loan.DueAtUtc < overdueIfDueBefore, ct);
            var dueToday = await Context.Loans.CountAsync(
                loan => loan.ReturnedAtUtc == null && loan.DueAtUtc >= overdueIfDueBefore && loan.DueAtUtc < end,
                ct
            );

            return new DeskData(pending, readyToRelease, overdue, dueToday);
        }

        public async Task<IReadOnlyList<Reservation>> PendingRequestsAsync(DateTime now, CancellationToken ct)
        {
            return await Context
                .Reservations.AsNoTracking()
                .Include(x => x.EquipmentItem)
                .Include(x => x.BorrowerProfile.User)
                .Where(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(5)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Loan>> DueLoansAsync(DateTime end, CancellationToken ct)
        {
            return await Context
                .Loans.AsNoTracking()
                .Include(x => x.Reservation.EquipmentItem)
                .Include(x => x.Reservation.BorrowerProfile.User)
                .Where(x => x.ReturnedAtUtc == null && x.DueAtUtc < end)
                .OrderByDescending(x => x.DueAtUtc)
                .Take(5)
                .ToListAsync(ct);
        }

        public Task<int> PendingRegistrationsAsync(CancellationToken ct)
        {
            return Context.Users.CountAsync(x => x.ApprovalStatus == AccountApprovalStatus.PendingApproval, ct);
        }

        public async Task<IReadOnlyList<Reservation>> RecentReservationsAsync(CancellationToken ct)
        {
            return await Context
                .Reservations.AsNoTracking()
                .Include(x => x.EquipmentItem)
                .Include(x => x.BorrowerProfile.User)
                .Include(x => x.Loan)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ThenByDescending(x => x.CreatedAtUtc)
                .Take(7)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<User>> RegistrationsPendingAsync(CancellationToken ct)
        {
            return await Context
                .Users.AsNoTracking()
                .Include(x => x.BorrowerProfile)
                .Where(x => x.ApprovalStatus == AccountApprovalStatus.PendingApproval)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(5)
                .ToListAsync(ct);
        }

        public Task<int> OpenCasesAsync(CancellationToken ct)
        {
            return Context.MaintenanceCases.CountAsync(x => x.Status != MaintenanceStatus.Closed, ct);
        }

        public async Task<IReadOnlyList<ValueCount<EquipmentCondition>>> ConditionsAsync(CancellationToken ct)
        {
            return await Context
                .EquipmentItems.AsNoTracking()
                .Where(x => x.IsActive)
                .GroupBy(x => x.Condition)
                .Select(x => new ValueCount<EquipmentCondition>(x.Key, x.Count()))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ValueCount<ReservationStatus>>> StatusesAsync(CancellationToken ct)
        {
            return await Context
                .Reservations.AsNoTracking()
                .GroupBy(x => x.Status)
                .Select(x => new ValueCount<ReservationStatus>(x.Key, x.Count()))
                .ToListAsync(ct);
        }

        public Task<int> LivePendingAsync(DateTime now, CancellationToken ct)
        {
            return Context.Reservations.CountAsync(
                x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now,
                ct
            );
        }

        public async Task<IReadOnlyList<DateTime>> RequestTimesAsync(DateTime fromUtc, CancellationToken ct)
        {
            return await Context
                .Reservations.AsNoTracking()
                .Where(x => x.CreatedAtUtc >= fromUtc)
                .Select(x => x.CreatedAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<DateTime>> ReleaseTimesAsync(DateTime fromUtc, CancellationToken ct)
        {
            return await Context.Loans.AsNoTracking().Where(x => x.ReleasedAtUtc >= fromUtc).Select(x => x.ReleasedAtUtc).ToListAsync(ct);
        }

        public async Task<IReadOnlyList<DateTime>> ReturnTimesAsync(DateTime fromUtc, CancellationToken ct)
        {
            return await Context
                .Loans.AsNoTracking()
                .Where(x => x.ReturnedAtUtc >= fromUtc)
                .Select(x => x.ReturnedAtUtc!.Value)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<DateTime>> RegistrationTimesAsync(DateTime fromUtc, CancellationToken ct)
        {
            return await Context
                .BorrowerProfiles.AsNoTracking()
                .Where(x => x.User.CreatedAtUtc >= fromUtc)
                .Select(x => x.User.CreatedAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Loan>> RecentLoansAsync(CancellationToken ct)
        {
            return await Context
                .Loans.AsNoTracking()
                .Include(x => x.Reservation.EquipmentItem)
                .Include(x => x.Reservation.BorrowerProfile.User)
                .OrderByDescending(x => x.ReturnedAtUtc ?? x.ReleasedAtUtc)
                .Take(6)
                .ToListAsync(ct);
        }
    }
}
