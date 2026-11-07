using CampusGear.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class AccountRepository(ApplicationDbContext db) : IAccountRepository
{
    public Task<bool> HasBorrowerRoleAsync(string userId, CancellationToken ct) => db.UserRoles.AsNoTracking().Join(db.Roles.AsNoTracking(),
        ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r }).AnyAsync(x => x.ur.UserId == userId && x.r.NormalizedName == "BORROWER", ct);
}
