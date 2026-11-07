using CampusGear.Data.Interfaces;
using CampusGear.Data.QueryModels;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class AccountRepository(ApplicationDbContext db) : IAccountRepository
{
    public Task<bool> HasBorrowerRoleAsync(string userId, CancellationToken ct) => db.UserRoles.AsNoTracking().Join(db.Roles.AsNoTracking(),
        ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r }).AnyAsync(x => x.ur.UserId == userId && x.r.NormalizedName == "BORROWER", ct);
    public async Task<AccountPageData> ListAsync(string search, string role, string status, int page, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (search.Length > 0) query = query.Where(x => x.FullName.Contains(search) || x.Email!.Contains(search) || x.BorrowerProfile != null &&
            (x.BorrowerProfile.StudentNumber != null && x.BorrowerProfile.StudentNumber.Contains(search) || x.BorrowerProfile.Department != null && x.BorrowerProfile.Department.Contains(search)));
        if (role != "all") query = query.Where(x => db.UserRoles.Any(ur => ur.UserId == x.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));
        query = status switch
        {
            "active" => query.Where(x => x.IsActive && x.ApprovalStatus == AccountApprovalStatus.Approved),
            "inactive" => query.Where(x => !x.IsActive),
            "pending" => query.Where(x => x.ApprovalStatus == AccountApprovalStatus.PendingApproval),
            "rejected" => query.Where(x => x.ApprovalStatus == AccountApprovalStatus.Rejected),
            _ => query
        };
        var total = await query.CountAsync(ct); var pending = await db.Users.CountAsync(x => x.ApprovalStatus == AccountApprovalStatus.PendingApproval, ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / 15d)); page = Math.Clamp(page, 1, pages);
        var accounts = await query.Include(x => x.BorrowerProfile).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.FullName).ThenBy(x => x.Id)
            .Skip((page - 1) * 15).Take(15).ToListAsync(ct);
        var ids = accounts.Select(x => x.Id).ToArray();
        var roles = await db.UserRoles.AsNoTracking().Where(x => ids.Contains(x.UserId)).Join(db.Roles, ur => ur.RoleId, r => r.Id,
            (ur, r) => new UserRoleRecord(ur.UserId, r.Name)).ToListAsync(ct);
        return new(accounts, roles, page, pages, total, pending);
    }
    public Task<ApplicationUser?> FindAsync(string id, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ApplicationUser?> FindFreshAsync(string id, CancellationToken ct) => db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ApplicationUser?> FindWithProfileAsync(string id, CancellationToken ct) => db.Users.Include(x => x.BorrowerProfile).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ApplicationUser?> FindWithProfileFreshAsync(string id, CancellationToken ct) => db.Users.AsNoTracking().Include(x => x.BorrowerProfile).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> StudentExistsAsync(string number, string? excludingUser, CancellationToken ct) => db.BorrowerProfiles.AnyAsync(x => x.StudentNumber == number && x.UserId != excludingUser, ct);
    public Task<bool> HasOtherAdministratorAsync(string excludingUser, CancellationToken ct) => db.UserRoles.AnyAsync(ur => ur.UserId != excludingUser &&
        db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Administrator") && db.Users.Any(u => u.Id == ur.UserId && u.IsActive && u.EmailConfirmed), ct);
    public async Task<bool> HasHistoryAsync(string userId, CancellationToken ct) =>
        await db.Reservations.AnyAsync(x => x.BorrowerProfile.UserId == userId || x.DecidedByUserId == userId, ct) ||
        await db.Loans.AnyAsync(x => x.ReleasedByUserId == userId || x.ReceivedByUserId == userId, ct) ||
        await db.MaintenanceCases.AnyAsync(x => x.OpenedByUserId == userId || x.ClosedByUserId == userId, ct);
    public async Task PrepareDeletionAsync(ApplicationUser user, CancellationToken ct)
    {
        await db.AuditEvents.Where(x => x.ActorUserId == user.Id).ExecuteUpdateAsync(x => x.SetProperty(e => e.ActorUserId, (string?)null), ct);
        await db.EmailChallenges.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(ct);
        if (user.BorrowerProfile is not null) db.BorrowerProfiles.Remove(user.BorrowerProfile);
    }
    public Task ReloadProfileAsync(BorrowerProfile profile, CancellationToken ct) => db.Entry(profile).ReloadAsync(ct);
    public async Task<bool> ReloadUserAsync(ApplicationUser user, CancellationToken ct)
    {
        await db.Entry(user).ReloadAsync(ct);
        return db.Entry(user).State != EntityState.Detached;
    }
    public void AddProfile(BorrowerProfile profile) => db.BorrowerProfiles.Add(profile);
}
