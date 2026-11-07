using CampusGear.Data.QueryModels;
using CampusGear.Services.ServiceModels;
namespace CampusGear.Services;
public interface IInventoryService
{
    Task<EquipmentRecord?> GetEditAsync(Guid id, CancellationToken ct);
    Task<InventoryPage> GetPageAsync(string? query, int page, CancellationToken ct);
    Task<OperationResult<Guid>> SaveAsync(RequestContext actor, EquipmentCommand input, UploadContent? imageFile, CancellationToken ct);
    Task<OperationResult<Guid>> DeleteAsync(RequestContext actor, Guid id, CancellationToken ct);
}
