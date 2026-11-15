using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IBorrowerProfileRepository
    {
        Task<Guid?> GetIdAsync(string userId, CancellationToken ct);
        Task<BorrowerProfile?> FindFreshAsync(Guid id, CancellationToken ct);
    }
}
