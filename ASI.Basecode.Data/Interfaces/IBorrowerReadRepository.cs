using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IBorrowerReadRepository
    {
        Task<BorrowerProfile?> FindProfileAsync(string? userId, CancellationToken ct);
        Task<IReadOnlyList<CatalogUnit>> CatalogAsync(CancellationToken ct);
        Task<IReadOnlyList<Guid>> InUseAsync(DateTime now, CancellationToken ct);
        Task<IReadOnlyList<RequestRecord>> FindRequestsAsync(Guid profileId, Guid[] ids, CancellationToken ct);
        Task<IReadOnlyList<Reservation>> SubmittedAsync(Guid profileId, IReadOnlyList<Guid> ids, CancellationToken ct);
        Task<BorrowerHomeData> HomeAsync(Guid profileId, DateTime now, CancellationToken ct);
        Task<ReservationRows> HistoryAsync(
            Guid profileId,
            string status,
            string? query,
            DateTime? fromUtc,
            DateTime? toUtc,
            DateTime now,
            int page,
            CancellationToken ct
        );
    }
}
