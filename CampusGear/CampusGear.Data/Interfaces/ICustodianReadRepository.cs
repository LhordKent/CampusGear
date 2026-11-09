using CampusGear.Data.QueryModels;
using CampusGear.Models;
namespace CampusGear.Data.Interfaces;
public interface ICustodianReadRepository
{
    Task<Reservation?> DetailsAsync(Guid id, CancellationToken ct);
    Task<CustodianRows> ListAsync(string queue, string status, string? search, DateTime? from, DateTime? to, DateTime now, int page, CancellationToken ct);
}
