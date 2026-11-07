using CampusGear.Data.Interfaces;
using CampusGear.Data.QueryModels;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class DashboardReadRepository(ApplicationDbContext db) : IDashboardReadRepository
{
    public async Task<DeskData> DeskAsync(DateTime now, DateTime end, CancellationToken ct) => new(
        await db.Reservations.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now, ct),
        await db.Reservations.CountAsync(x => x.Status == ReservationStatus.Approved && x.StartAtUtc <= now && x.EndAtUtc > now, ct),
        await db.Loans.CountAsync(x => x.ReturnedAtUtc == null && x.DueAtUtc < now, ct),
        await db.Loans.CountAsync(x => x.ReturnedAtUtc == null && x.DueAtUtc >= now && x.DueAtUtc < end, ct));
    public async Task<IReadOnlyList<Reservation>> PendingRequestsAsync(DateTime now, CancellationToken ct) => await db.Reservations.AsNoTracking()
        .Include(x => x.EquipmentItem).Include(x => x.BorrowerProfile.User)
        .Where(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now)
        .OrderByDescending(x => x.CreatedAtUtc).Take(5).ToListAsync(ct);
    public async Task<IReadOnlyList<Loan>> DueLoansAsync(DateTime end, CancellationToken ct) => await db.Loans.AsNoTracking()
        .Include(x => x.Reservation.EquipmentItem).Include(x => x.Reservation.BorrowerProfile.User)
        .Where(x => x.ReturnedAtUtc == null && x.DueAtUtc < end).OrderByDescending(x => x.DueAtUtc).Take(5).ToListAsync(ct);
    public Task<int> PendingRegistrationsAsync(CancellationToken ct) => db.Users.CountAsync(x => x.ApprovalStatus == AccountApprovalStatus.PendingApproval, ct);
    public async Task<IReadOnlyList<Reservation>> RecentReservationsAsync(CancellationToken ct) => await db.Reservations.AsNoTracking()
        .Include(x => x.EquipmentItem).Include(x => x.BorrowerProfile.User).Include(x => x.Loan)
        .OrderByDescending(x => x.UpdatedAtUtc).ThenByDescending(x => x.CreatedAtUtc).Take(7).ToListAsync(ct);
    public async Task<IReadOnlyList<ApplicationUser>> RegistrationsPendingAsync(CancellationToken ct) => await db.Users.AsNoTracking().Include(x => x.BorrowerProfile)
        .Where(x => x.ApprovalStatus == AccountApprovalStatus.PendingApproval).OrderByDescending(x => x.CreatedAtUtc).Take(5).ToListAsync(ct);
    public Task<int> OpenCasesAsync(CancellationToken ct) => db.MaintenanceCases.CountAsync(x => x.Status != MaintenanceStatus.Closed, ct);
    public async Task<IReadOnlyList<ValueCount<EquipmentCondition>>> ConditionsAsync(CancellationToken ct) => await db.EquipmentItems.AsNoTracking().Where(x => x.IsActive)
        .GroupBy(x => x.Condition).Select(x => new ValueCount<EquipmentCondition>(x.Key, x.Count())).ToListAsync(ct);
    public async Task<IReadOnlyList<ValueCount<ReservationStatus>>> StatusesAsync(CancellationToken ct) => await db.Reservations.AsNoTracking()
        .GroupBy(x => x.Status).Select(x => new ValueCount<ReservationStatus>(x.Key, x.Count())).ToListAsync(ct);
    public Task<int> LivePendingAsync(DateTime now, CancellationToken ct) => db.Reservations.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now, ct);
    public async Task<IReadOnlyList<DateTime>> RequestTimesAsync(DateTime fromUtc, CancellationToken ct) => await db.Reservations.AsNoTracking().Where(x => x.CreatedAtUtc >= fromUtc).Select(x => x.CreatedAtUtc).ToListAsync(ct);
    public async Task<IReadOnlyList<DateTime>> ReleaseTimesAsync(DateTime fromUtc, CancellationToken ct) => await db.Loans.AsNoTracking().Where(x => x.ReleasedAtUtc >= fromUtc).Select(x => x.ReleasedAtUtc).ToListAsync(ct);
    public async Task<IReadOnlyList<DateTime>> ReturnTimesAsync(DateTime fromUtc, CancellationToken ct) => await db.Loans.AsNoTracking().Where(x => x.ReturnedAtUtc >= fromUtc).Select(x => x.ReturnedAtUtc!.Value).ToListAsync(ct);
    public async Task<IReadOnlyList<DateTime>> RegistrationTimesAsync(DateTime fromUtc, CancellationToken ct) => await db.BorrowerProfiles.AsNoTracking().Where(x => x.User.CreatedAtUtc >= fromUtc).Select(x => x.User.CreatedAtUtc).ToListAsync(ct);
    public async Task<IReadOnlyList<Loan>> RecentLoansAsync(CancellationToken ct) => await db.Loans.AsNoTracking().Include(x => x.Reservation.EquipmentItem)
        .Include(x => x.Reservation.BorrowerProfile.User).OrderByDescending(x => x.ReturnedAtUtc ?? x.ReleasedAtUtc).Take(6).ToListAsync(ct);
}
