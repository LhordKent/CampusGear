using CampusGear.Models;
using CampusGear.Services.ServiceModels;
namespace CampusGear.Controllers;

public sealed class ReservationPageViewModel(
    ReservationCommand input, IReadOnlyList<CatalogEquipment> items, bool isEligible)
{
    public ReservationCommand Input { get; } = input;
    public IReadOnlyList<CatalogEquipment> Items { get; } = items;
    public bool IsEligible { get; } = isEligible;
    public IReadOnlyList<string> Categories => Items.Select(x => x.Category).Distinct().Order().ToList();
    public CatalogEquipment? Selected => Items.FirstOrDefault(x => x.UnitIds.Contains(Input.EquipmentItemId));
    public IReadOnlyList<SubmittedReservation> Submitted { get; set; } = [];
    public string? Feedback { get; set; }
    public bool IsConflict { get; set; }
    public bool OpenDialog { get; set; }
    public IReadOnlyList<Guid> CompletedIds { get; set; } = [];
    /// <summary>The earliest borrowing time the date pickers offer, in Manila time.</summary>
    public string MinStartLocal { get; init; } = string.Empty;
}

/// <summary>One catalog product: every active unit that shares a category and name.</summary>
/// <param name="Id">The unit the request form posts; the server picks the actual free units.</param>
/// <param name="FreeNowUnits">Bookable units not on loan or inside an approved booking right now.</param>
public sealed record CatalogEquipment(
    Guid Id, string Name, string Category, string? Description, string? ImagePath,
    IReadOnlyList<string> Locations, IReadOnlyList<EquipmentCondition> Conditions,
    IReadOnlyList<Guid> UnitIds, IReadOnlyList<Guid> BookableUnitIds, int FreeNowUnits,
    DateTime AddedAtUtc)
{
    public int TotalUnits => UnitIds.Count;
    public int BookableUnits => BookableUnitIds.Count;
    public bool IsRequestable => BookableUnits > 0;
    public int MaxQuantity => Math.Min(BookableUnits, BorrowingLimits.MaxUnitsPerRequest);
    public string Availability => !IsRequestable ? "Unavailable" : FreeNowUnits == 0 ? "In use" : "Available";
    public string AvailabilityDetail => !IsRequestable
        ? "Under maintenance"
        : FreeNowUnits == 0
            ? "All units are out now · reserve a later time"
            : $"{FreeNowUnits} of {TotalUnits} {(TotalUnits == 1 ? "unit" : "units")} ready now";
    public string ConditionText => Conditions.Count == 0 ? "Under maintenance" : string.Join(" · ", Conditions);
    public string LocationText => Locations.Count switch
    {
        0 => "Not specified",
        1 => Locations[0],
        _ => string.Join(", ", Locations)
    };
}

public sealed record HomeCategory(string Name, int Products, int ReadyUnits);
public sealed record HomeReservation(Guid Id, string EquipmentName, string AssetCode,
    string StartManila, string EndManila, string Status);
public sealed record BorrowerHomeViewModel(string FirstName, bool IsEligible,
    IReadOnlyList<CatalogEquipment> RecentlyAdded, IReadOnlyList<CatalogEquipment> ReadyNow,
    IReadOnlyList<HomeCategory> Categories, int ProductCount, int ReadyUnitCount,
    IReadOnlyList<HomeReservation> Upcoming, int PendingCount, int ApprovedCount, int BorrowedCount,
    int OverdueCount, int CompletedCount);

public sealed record SubmittedReservation(Guid Id, string EquipmentName, string AssetCode,
    string StartManila, string EndManila, string? HoldExpiresManila, ReservationStatus Status);

public sealed record HistoryRow(Guid Id, string EquipmentName, string AssetCode,
    string StartManila, string EndManila, string Status, string Purpose,
    bool CanCancel, string? ReturnedManila, string? ReturnCondition);

public sealed record HistoryPageViewModel(IReadOnlyList<HistoryRow> Rows,
    string Status, string From, string To, string Query, int Page, int PageCount, int Total,
    string? Feedback);

public sealed record CalendarEquipment(Guid Id, string Name, string AssetCode, bool IsMaintenanceHold);
public sealed record CalendarDay(DateOnly Date, bool IsCurrentMonth, bool IsToday,
    bool IsSelected, string Status, int BookingCount);
public sealed record CalendarSlot(string StartManila, string EndManila, string Status);
public sealed record CalendarPageViewModel(
    IReadOnlyList<CalendarEquipment> Items, CalendarEquipment? SelectedItem,
    int Year, int Month, string MonthTitle, DateOnly PreviousMonth, DateOnly NextMonth,
    IReadOnlyList<CalendarDay> Days, DateOnly SelectedDate,
    IReadOnlyList<CalendarSlot> SelectedSlots, IReadOnlyList<EquipmentPickerItem> Picker);

/// <summary>Picks the catalog placeholder icon (a symbol in the catalog sprite) from an item's name and category.</summary>
