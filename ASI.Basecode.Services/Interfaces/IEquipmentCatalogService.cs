using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IEquipmentCatalogService
    {
        Task<OperationResult<BorrowerHomeViewModel>> HomeAsync(RequestContext actor, CancellationToken cancellationToken);
        Task<OperationResult<ReservationPageViewModel>> ReservationAsync(
            RequestContext actor,
            Guid? equipmentItemId,
            string? state,
            Guid[]? id,
            CancellationToken cancellationToken
        );
        Task<OperationResult<AvailabilityResult>> AvailabilityAsync(
            RequestContext actor,
            Guid equipmentItemId,
            string? start,
            string? end,
            CancellationToken cancellationToken
        );
        Task<OperationResult<ReservationSchedule>> ScheduleAsync(
            RequestContext actor,
            Guid equipmentItemId,
            string? from,
            CancellationToken cancellationToken
        );
        Task<ReservationPageViewModel> GetRequestPageAsync(
            bool isEligible,
            ReservationRequestModel input,
            CancellationToken cancellationToken
        );
    }
}
