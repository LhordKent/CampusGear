using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IEquipmentRepository
    {
        Task<EquipmentItem?> FindAsync(Guid id, CancellationToken ct);
        Task<bool> HasHistoryAsync(Guid id, CancellationToken ct);
        Task<bool> HasOutstandingLoanAsync(Guid id, CancellationToken ct);
        Task<bool> HasLiveReservationsAsync(Guid id, DateTime now, CancellationToken ct);
        Task<InventoryData> InventoryAsync(string? query, int page, CancellationToken ct);
        void Add(EquipmentItem item);
        void Remove(EquipmentItem item);
        void MarkUpdated(EquipmentItem item);
    }
}
