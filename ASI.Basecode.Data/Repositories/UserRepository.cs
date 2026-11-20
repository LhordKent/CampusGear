using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public sealed class UserRepository : BaseRepository, IUserRepository
    {
        public UserRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        public Task<bool> HasBorrowerRoleAsync(string userId, CancellationToken ct)
        {
            return Context
                .UserRoles.AsNoTracking()
                .Join(Context.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r })
                .AnyAsync(x => x.ur.UserId == userId && x.r.NormalizedName == "BORROWER", ct);
        }

        public async Task<AccountPageData> ListAsync(string search, string role, string status, int page, CancellationToken ct)
        {
            var query = Context.Users.AsNoTracking();
            if (search.Length > 0)
            {
                query = query.Where(x =>
                    x.FullName.Contains(search)
                    || x.Email!.Contains(search)
                    || x.BorrowerProfile != null
                        && (
                            x.BorrowerProfile.StudentNumber != null && x.BorrowerProfile.StudentNumber.Contains(search)
                            || x.BorrowerProfile.Department != null && x.BorrowerProfile.Department.Contains(search)
                        )
                );
            }

            if (role != "all")
            {
                query = query.Where(x =>
                    Context.UserRoles.Any(ur => ur.UserId == x.Id && Context.Roles.Any(r => r.Id == ur.RoleId && r.Name == role))
                );
            }

            query = status switch
            {
                "active" => query.Where(x => x.IsActive && x.ApprovalStatus == AccountApprovalStatus.Approved),
                "inactive" => query.Where(x => !x.IsActive),
                "pending" => query.Where(x => x.ApprovalStatus == AccountApprovalStatus.PendingApproval),
                "rejected" => query.Where(x => x.ApprovalStatus == AccountApprovalStatus.Rejected),
                _ => query,
            };
            var total = await query.CountAsync(ct);
            var pending = await Context.Users.CountAsync(x => x.ApprovalStatus == AccountApprovalStatus.PendingApproval, ct);
            var pages = Math.Max(1, (int)Math.Ceiling(total / 15d));
            page = Math.Clamp(page, 1, pages);
            var accounts = await query
                .Include(x => x.BorrowerProfile)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenBy(x => x.FullName)
                .ThenBy(x => x.Id)
                .Skip((page - 1) * 15)
                .Take(15)
                .ToListAsync(ct);
            var ids = accounts.Select(x => x.Id).ToArray();
            var roles = await Context
                .UserRoles.AsNoTracking()
                .Where(x => ids.Contains(x.UserId))
                .Join(Context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new UserRoleRecord(ur.UserId, r.Name))
                .ToListAsync(ct);
            return new(accounts, roles, page, pages, total, pending);
        }

        public Task<User?> FindAsync(string id, CancellationToken ct)
        {
            return Context.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        }

        public Task<User?> FindFreshAsync(string id, CancellationToken ct)
        {
            return Context.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        }

        public Task<User?> FindWithProfileAsync(string id, CancellationToken ct)
        {
            return Context.Users.Include(x => x.BorrowerProfile).SingleOrDefaultAsync(x => x.Id == id, ct);
        }

        public Task<User?> FindWithProfileFreshAsync(string id, CancellationToken ct)
        {
            return Context.Users.AsNoTracking().Include(x => x.BorrowerProfile).SingleOrDefaultAsync(x => x.Id == id, ct);
        }

        public Task<bool> StudentExistsAsync(string number, string? excludingUser, CancellationToken ct)
        {
            return Context.BorrowerProfiles.AnyAsync(x => x.StudentNumber == number && x.UserId != excludingUser, ct);
        }

        public Task<bool> HasOtherAdministratorAsync(string excludingUser, CancellationToken ct)
        {
            return Context.UserRoles.AnyAsync(
                ur =>
                    ur.UserId != excludingUser
                    && Context.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Administrator)
                    && Context.Users.Any(u => u.Id == ur.UserId && u.IsActive && u.EmailConfirmed),
                ct
            );
        }

        public async Task<bool> HasHistoryAsync(string userId, CancellationToken ct)
        {
            return await Context.Reservations.AnyAsync(x => x.BorrowerProfile.UserId == userId || x.DecidedByUserId == userId, ct)
                || await Context.Loans.AnyAsync(x => x.ReleasedByUserId == userId || x.ReceivedByUserId == userId, ct)
                || await Context.MaintenanceCases.AnyAsync(x => x.OpenedByUserId == userId || x.ClosedByUserId == userId, ct);
        }

        // Released = the equipment is physically with the borrower; a return moves it to Completed.
        public Task<bool> HasOpenLoanAsync(string userId, CancellationToken ct)
        {
            return Context.Reservations.AnyAsync(
                x => x.BorrowerProfile.UserId == userId && x.Status == ReservationStatus.Released,
                ct
            );
        }

        // Pending and approved bookings have not left the desk yet, so they can be cancelled. Tracked for update.
        public async Task<IReadOnlyList<Reservation>> OpenBookingsAsync(string userId, CancellationToken ct)
        {
            return await Context
                .Reservations.Where(x =>
                    x.BorrowerProfile.UserId == userId
                    && (x.Status == ReservationStatus.Pending || x.Status == ReservationStatus.Approved)
                )
                .ToListAsync(ct);
        }

        public async Task PrepareDeletionAsync(User user, CancellationToken ct)
        {
            await Context
                .AuditEvents.Where(x => x.ActorUserId == user.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(e => e.ActorUserId, (string?)null), ct);
            await Context.EmailChallenges.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(ct);
            if (user.BorrowerProfile is not null)
            {
                Context.BorrowerProfiles.Remove(user.BorrowerProfile);
            }
        }

        public Task ReloadProfileAsync(BorrowerProfile profile, CancellationToken ct)
        {
            return Context.Entry(profile).ReloadAsync(ct);
        }

        public async Task<bool> ReloadUserAsync(User user, CancellationToken ct)
        {
            await Context.Entry(user).ReloadAsync(ct);
            return Context.Entry(user).State != EntityState.Detached;
        }

        public void AddProfile(BorrowerProfile profile) => Context.BorrowerProfiles.Add(profile);
    }
}
