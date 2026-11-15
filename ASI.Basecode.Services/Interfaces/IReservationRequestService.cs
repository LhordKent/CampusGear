using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IReservationRequestService
    {
        Task<OperationResult<ReservationPageViewModel>> CreateReservationAsync(
            RequestContext actor,
            ReservationRequestModel input,
            CancellationToken cancellationToken
        );
    }
}
