using CampusGear.Models;
namespace CampusGear.Services;
public interface IEquipmentAvailabilityService
{
    Task<Dictionary<Guid, ItemStatus>> GetStatesAsync(DateTime nowUtc, CancellationToken ct);
    Task<IReadOnlyList<EquipmentPickerItem>> GetPickerAsync(DateTime nowUtc, CancellationToken ct);
}
