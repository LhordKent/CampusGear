using CampusGear.Data.QueryModels;
using CampusGear.Models;
namespace CampusGear.Data.Interfaces;
public interface ICategoryRepository
{
    Task<EquipmentCategory?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<CategoryList> ListAsync(string? query, CancellationToken cancellationToken);
    Task<bool> HasItemsAsync(Guid id, bool activeOnly, CancellationToken cancellationToken);
    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<EquipmentCategory>> ActiveAsync(CancellationToken cancellationToken);
    void Add(EquipmentCategory category);
    void Remove(EquipmentCategory category);
}
