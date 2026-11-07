using CampusGear.Services.ServiceModels;
namespace CampusGear.Services;
public interface ICategoryService
{
    Task<CategoryCommand?> GetEditAsync(Guid id, CancellationToken ct);
    Task<CategoryPage> GetPageAsync(string? query, CancellationToken ct);
    Task<OperationResult<Guid>> SaveAsync(RequestContext actor, CategoryCommand input, CancellationToken ct);
    Task<OperationResult<Guid>> DeleteAsync(RequestContext actor, Guid id, CancellationToken ct);
    Task<OperationResult<Guid>> AddFromInventoryAsync(RequestContext actor, string? name, CancellationToken ct);
}
