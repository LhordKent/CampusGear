using CampusGear.Services.ServiceModels;
namespace CampusGear.Services;
public interface IMaintenanceService
{
    Task<MaintenancePage> GetPageAsync(string? status, string? query, int page, CancellationToken ct);
    Task<OperationResult<bool>> OpenAsync(RequestContext actor, Guid equipmentItemId, string? description, CancellationToken ct);
    Task<OperationResult<bool>> UpdateAsync(RequestContext actor, Guid id, MaintenanceCommand input, CancellationToken ct);
}
