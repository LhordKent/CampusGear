using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
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
}
