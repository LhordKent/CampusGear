using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

// BORROWER PROFILE REPOSITORY  -  a student's extra details: student number, department, "can reserve" flag.
namespace ASI.Basecode.Data.Repositories
{
    public sealed class BorrowerProfileRepository : BaseRepository, IBorrowerProfileRepository
    {
        public BorrowerProfileRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
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
