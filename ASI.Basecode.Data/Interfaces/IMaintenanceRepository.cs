using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IMaintenanceRepository
    {
        Task<bool> HasOpenAsync(Guid itemId, CancellationToken ct, Guid? excludingCase = null);
        void Add(MaintenanceCase item);
        Task<Guid?> GetItemIdAsync(Guid id, CancellationToken ct);
        Task<MaintenanceCase> FindWithItemAsync(Guid id, CancellationToken ct);
        Task<MaintenanceListData> ListAsync(string status, string? query, int page, CancellationToken ct);
    }
}
