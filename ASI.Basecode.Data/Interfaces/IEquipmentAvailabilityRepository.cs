using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IEquipmentAvailabilityRepository
    {
        Task<IReadOnlyList<Guid>> BlockedAsync(IReadOnlyList<Guid> ids, DateTime start, DateTime end, DateTime now, CancellationToken ct);
        Task<IReadOnlyList<CalendarItemData>> CalendarItemsAsync(CancellationToken ct);
        Task<IReadOnlyList<Reservation>> CalendarBookingsAsync(
            Guid itemId,
            DateTime fromUtc,
            DateTime toUtc,
            DateTime now,
            bool staff,
            CancellationToken ct
        );
        Task<IReadOnlyList<EquipmentStateData>> EquipmentStatesAsync(DateTime now, CancellationToken ct);
    }
}
