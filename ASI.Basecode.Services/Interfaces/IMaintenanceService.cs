using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IMaintenanceService
    {
        Task<MaintenancePage> GetPageAsync(string? status, string? query, int page, CancellationToken ct);
        Task<OperationResult<bool>> OpenAsync(RequestContext actor, Guid equipmentItemId, string? description, CancellationToken ct);
        Task<OperationResult<bool>> UpdateAsync(RequestContext actor, Guid id, MaintenanceEditModel input, CancellationToken ct);
    }
}
