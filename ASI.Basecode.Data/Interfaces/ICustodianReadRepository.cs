using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface ICustodianReadRepository
    {
        Task<Reservation?> DetailsAsync(Guid id, CancellationToken ct);
        Task<CustodianRows> ListAsync(
            string queue,
            string status,
            string? search,
            DateTime? from,
            DateTime? to,
            DateTime now,
            int page,
            CancellationToken ct
        );
    }
}
