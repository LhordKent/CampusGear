using CampusGear.Data.QueryModels;
using CampusGear.Models;
namespace CampusGear.Data.Interfaces;
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
