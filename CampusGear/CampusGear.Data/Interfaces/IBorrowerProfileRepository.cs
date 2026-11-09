using CampusGear.Models;
namespace CampusGear.Data.Interfaces;
public interface IBorrowerProfileRepository
{
    Task<Guid?> GetIdAsync(string userId, CancellationToken ct);
    Task<BorrowerProfile?> FindFreshAsync(Guid id, CancellationToken ct);
}
