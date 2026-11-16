using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Models
{
    public sealed record CatalogUnit(
        Guid Id,
        Guid CategoryId,
        string Category,
        string Name,
        string? Description,
        string? ImagePath,
        string? Location,
        EquipmentCondition Condition,
        DateTime CreatedAtUtc,
        bool IsBookable,
        string AssetCode
    );

    public sealed record RequestRecord(Guid Id, ReservationStatus Status);

    public sealed record BorrowerHomeData(
        IReadOnlyList<Reservation> Upcoming,
        int Pending,
        int Approved,
        int Borrowed,
        int Overdue,
        int Completed
    );

    public sealed record ReservationRows(IReadOnlyList<Reservation> Rows, int Page, int Pages, int Total);

    public sealed record CalendarItemData(
        Guid Id,
        string Name,
        string AssetCode,
        bool MaintenanceHold,
        Guid CategoryId,
        string Category,
        string? ImagePath
    );

    public sealed record EquipmentStateData(Guid Id, bool IsActive, bool Unusable, bool Borrowed, bool Reserved);

    /// <summary>A time a unit cannot be booked: an approved or released booking, or a live pending hold.</summary>
    public sealed record BusyIntervalData(Guid EquipmentItemId, DateTime StartAtUtc, DateTime EndAtUtc);
}
