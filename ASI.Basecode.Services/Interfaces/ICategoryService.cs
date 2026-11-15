using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<CategoryEditModel?> GetEditAsync(Guid id, CancellationToken ct);
        Task<CategoryPage> GetPageAsync(string? query, CancellationToken ct);
        Task<OperationResult<Guid>> SaveAsync(RequestContext actor, CategoryEditModel input, CancellationToken ct);
        Task<OperationResult<Guid>> DeleteAsync(RequestContext actor, Guid id, CancellationToken ct);
        Task<OperationResult<Guid>> AddFromInventoryAsync(RequestContext actor, string? name, CancellationToken ct);
    }
}
