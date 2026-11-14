using CampusGear.Data.Interfaces;
using CampusGear.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Data.Repositories
{
    public sealed class BorrowerProfileRepository : BaseRepository, IBorrowerProfileRepository
    {
        public BorrowerProfileRepository(IUnitOfWork unitOfWork, ApplicationDbContext db)
            : base(unitOfWork, db) { }

        public Task<Guid?> GetIdAsync(string userId, CancellationToken ct)
        {
            return Context.BorrowerProfiles.AsNoTracking().Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        }

        public Task<BorrowerProfile?> FindFreshAsync(Guid id, CancellationToken ct)
        {
            return Context.BorrowerProfiles.AsNoTracking().Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id, ct);
        }
    }
}
