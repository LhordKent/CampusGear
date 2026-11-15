using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ICustodianQueueService
    {
        Task<CustodianDetailViewModel?> GetDetailsAsync(Guid id, bool custodian, CancellationToken cancellationToken);
        Task<CustodianListViewModel> GetApprovalQueueAsync(CustodianQueueFilter filter, CancellationToken cancellationToken);
        Task<CustodianListViewModel> GetReturnQueueAsync(CustodianQueueFilter filter, CancellationToken cancellationToken);
        Task<CustodianListViewModel> GetHistoryAsync(CustodianQueueFilter filter, CancellationToken cancellationToken);
    }
}
