using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IInventoryService
    {
        Task<EquipmentRecord?> GetEditAsync(Guid id, CancellationToken ct);
        Task<InventoryPage> GetPageAsync(string? query, int page, CancellationToken ct);
        Task<OperationResult<Guid>> SaveAsync(
            RequestContext actor,
            EquipmentEditModel input,
            UploadContent? imageFile,
            CancellationToken ct
        );
        Task<OperationResult<Guid>> DeleteAsync(RequestContext actor, Guid id, CancellationToken ct);
    }
}
