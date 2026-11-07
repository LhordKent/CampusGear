using CampusGear.Data.Interfaces;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class BorrowerProfileRepository(ApplicationDbContext db) : IBorrowerProfileRepository
{
    public Task<Guid?> GetIdAsync(string userId, CancellationToken ct) => db.BorrowerProfiles.AsNoTracking()
        .Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    public Task<BorrowerProfile?> FindFreshAsync(Guid id, CancellationToken ct) => db.BorrowerProfiles.AsNoTracking()
        .Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id, ct);
}
