using CampusGear.Data.QueryModels;

namespace CampusGear.Services.ServiceModels
{
    public sealed record CustodianCalendarDay(DateOnly Date, bool CurrentMonth, bool Today, bool Selected, string Status, int BookingCount);

    public sealed record CustodianCalendarItem(Guid Id, string Name, string AssetCode, bool MaintenanceHold);

    public sealed record CustodianCalendarSlot(Guid Id, string BorrowerName, string StartManila, string EndManila, string Status);

    public sealed record CustodianCalendarViewModel(
        IReadOnlyList<CustodianCalendarItem> Items,
        CustodianCalendarItem? SelectedItem,
        DateOnly Month,
        IReadOnlyList<CustodianCalendarDay> Days,
        DateOnly SelectedDay,
        IReadOnlyList<CustodianCalendarSlot> Slots,
        IReadOnlyList<EquipmentPickerItem> Picker
    );

    public sealed record CustodianDashboardViewModel(
        string FirstName,
        DeskCounts Desk,
        IReadOnlyList<ActivityItem> PendingRequests,
        IReadOnlyList<ActivityItem> LateLoans,
        SeriesChart DeskActivity,
        DonutChart Availability,
        BarChart Condition,
        int OpenMaintenanceCases,
        IReadOnlyList<ActivityItem> RecentLoans
    );

    public sealed record CustodianDetailViewModel(
        ReservationRecord Record,
        string Status,
        bool CanApprove,
        bool CanReject,
        bool CanRelease,
        bool CanReturn,
        string? AvailabilityNote,
        string? Feedback,
        string? Error
    );

    public sealed record CustodianListRow(
        Guid Id,
        string BorrowerName,
        string BorrowerEmail,
        string EquipmentName,
        string AssetCode,
        string StartManila,
        string EndManila,
        string Status,
        string? ReturnCondition,
        string? ReturnedManila
    );

    public sealed record CustodianListViewModel(
        string Search,
        string Status,
        string From,
        string To,
        string Queue,
        int Page,
        int PageCount,
        int Total,
        IReadOnlyList<CustodianListRow> Rows,
        int PendingCount,
        int ApprovedCount,
        int ActiveCount,
        string? Feedback,
        string? Error
    );

    public sealed record CustodianQueueFilter(
        string? Search = null,
        string? Status = null,
        string? From = null,
        string? To = null,
        int Page = 1
    );

    /// <summary>What waits at the custodian desk; the dashboard header and its buttons show these.</summary>
    public sealed record DeskCounts(int Pending, int ReadyToRelease, int Overdue, int DueToday)
    {
        public int Waiting => Pending + ReadyToRelease + Overdue + DueToday;
    }
}
