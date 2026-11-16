using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IEquipmentAvailabilityService
    {
        Task<Dictionary<Guid, ItemStatus>> GetStatesAsync(DateTime nowUtc, CancellationToken ct);
        Task<IReadOnlyList<EquipmentPickerItem>> GetPickerAsync(DateTime nowUtc, CancellationToken ct);
        Task<List<Guid>> FindFreeUnitsAsync(
            IReadOnlyList<Guid> unitIds,
            DateTime startUtc,
            DateTime endUtc,
            DateTime now,
            Guid preferredUnitId,
            CancellationToken cancellationToken
        );
        Task<IReadOnlyList<BusyIntervalData>> BusyIntervalsAsync(
            IReadOnlyList<Guid> unitIds,
            DateTime fromUtc,
            DateTime toUtc,
            DateTime now,
            CancellationToken cancellationToken
        );
    }
}
