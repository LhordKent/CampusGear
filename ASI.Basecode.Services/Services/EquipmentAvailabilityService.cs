using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

// =====================================================================
// EQUIPMENT AVAILABILITY SERVICE  -  "what state is each item in right now?"
// Each item gets ONE state: Inactive, Borrowed (on loan), ForRepair (damaged / maintenance hold),
// Reserved (approved booking now), or Available.
// =====================================================================
namespace ASI.Basecode.Services.Services
{
    public sealed class EquipmentAvailabilityService : ServiceBase, IEquipmentAvailabilityService
    {
        private readonly IEquipmentAvailabilityRepository _reads;

        public EquipmentAvailabilityService(IEquipmentAvailabilityRepository reads)
        {
            _reads = reads;
        }

        // Works out the current state of every item (see the list above).
        public async Task<Dictionary<Guid, ItemStatus>> GetStatesAsync(DateTime nowUtc, CancellationToken ct)
        {
            var items = await _reads.EquipmentStatesAsync(nowUtc, ct);
            return items.ToDictionary(item => item.Id, GetStatus);
        }

        private static ItemStatus GetStatus(EquipmentStateData item)
        {
            if (!item.IsActive)
            {
                return ItemStatus.Inactive;
            }

            if (item.Borrowed)
            {
                return ItemStatus.Borrowed;
            }

            if (item.Unusable)
            {
                return ItemStatus.ForRepair;
            }

            return item.Reserved ? ItemStatus.Reserved : ItemStatus.Available;
        }

        // The list of items for staff pickers, each with its current state.
        public async Task<IReadOnlyList<EquipmentPickerItem>> GetPickerAsync(DateTime nowUtc, CancellationToken ct)
        {
            var states = await GetStatesAsync(nowUtc, ct);
            return (await _reads.CalendarItemsAsync(ct))
                .Select(x => new EquipmentPickerItem(
                    x.Id,
                    x.Name,
                    x.AssetCode,
                    x.Category,
                    x.ImagePath,
                    states.GetValueOrDefault(x.Id, ItemStatus.Available)
                ))
                .ToList();
        }

        // WBS 25.00 | Filter out blocked physical units and try the selected unit first if it is free.
        public async Task<List<Guid>> FindFreeUnitsAsync(
            IReadOnlyList<Guid> unitIds,
            DateTime startUtc,
            DateTime endUtc,
            DateTime now,
            Guid preferredUnitId,
            CancellationToken cancellationToken
        )
        {
            if (unitIds.Count == 0)
            {
                return [];
            }

            var blocked = await _reads.BlockedAsync(unitIds, startUtc, endUtc, now, cancellationToken);
            return unitIds.Where(id => !blocked.Contains(id)).OrderByDescending(id => id == preferredUnitId).ToList();
        }

        // Busy time ranges for items (used by the booking date picker).
        public async Task<IReadOnlyList<BusyIntervalData>> BusyIntervalsAsync(
            IReadOnlyList<Guid> unitIds,
            DateTime fromUtc,
            DateTime toUtc,
            DateTime now,
            CancellationToken cancellationToken
        )
        {
            return unitIds.Count == 0 ? [] : await _reads.BusyIntervalsAsync(unitIds, fromUtc, toUtc, now, cancellationToken);
        }
    }
}
