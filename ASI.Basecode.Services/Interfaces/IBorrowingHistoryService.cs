using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IBorrowingHistoryService
    {
        Task<OperationResult<HistoryPageViewModel>> HistoryAsync(
            RequestContext actor,
            string? status,
            string? from,
            string? to,
            string? q,
            int page = 1,
            CancellationToken cancellationToken = default
        );
    }
}
