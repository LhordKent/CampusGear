using CampusGear.Data.Interfaces;
using CampusGear.Models;
namespace CampusGear.Services;
public sealed class EquipmentAvailabilityService(IBorrowerReadRepository reads) : IEquipmentAvailabilityService
{
    public async Task<Dictionary<Guid, ItemStatus>> GetStatesAsync(DateTime nowUtc, CancellationToken ct) =>
        (await reads.EquipmentStatesAsync(nowUtc, ct)).ToDictionary(x => x.Id, x => !x.IsActive ? ItemStatus.Inactive :
            x.Borrowed ? ItemStatus.Borrowed : x.Unusable ? ItemStatus.ForRepair : x.Reserved ? ItemStatus.Reserved : ItemStatus.Available);
    public async Task<IReadOnlyList<EquipmentPickerItem>> GetPickerAsync(DateTime nowUtc, CancellationToken ct)
    {
        var states = await GetStatesAsync(nowUtc, ct);
        return (await reads.CalendarItemsAsync(ct)).Select(x => new EquipmentPickerItem(x.Id, x.Name, x.AssetCode, x.Category, x.ImagePath,
            states.GetValueOrDefault(x.Id, ItemStatus.Available))).ToList();
    }
}
