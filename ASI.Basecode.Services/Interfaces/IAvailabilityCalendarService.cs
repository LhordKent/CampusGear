using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAvailabilityCalendarService
    {
        Task<OperationResult<CalendarPageViewModel>> CalendarAsync(
            RequestContext actor,
            int? year,
            int? month,
            Guid? equipmentItemId,
            string? day,
            CancellationToken cancellationToken
        );
        Task<CustodianCalendarViewModel> GetStaffCalendarAsync(
            int? year,
            int? month,
            Guid? equipmentItemId,
            string? day,
            CancellationToken cancellationToken
        );
    }
}
