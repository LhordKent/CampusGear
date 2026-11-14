using System.Globalization;
using CampusGear.Models;
using CampusGear.Data.QueryModels;
namespace CampusGear.Controllers;
public sealed record CustodianListRow(Guid Id, string BorrowerName, string BorrowerEmail,
    string EquipmentName, string AssetCode, string StartManila, string EndManila,
    string Status, string? ReturnCondition, string? ReturnedManila);
public sealed record CustodianListViewModel(string Title, string ActionName,
    string Search, string Status, string From, string To, string Queue, int Page, int PageCount,
    int Total, IReadOnlyList<CustodianListRow> Rows, int PendingCount, int ApprovedCount, int ActiveCount,
    string? Feedback, string? Error);
public sealed record CustodianDetailViewModel(ReservationRecord Record, string Status, bool CanApprove,
    bool CanReject, bool CanRelease, bool CanReturn, string? AvailabilityNote, string? Feedback, string? Error);
public sealed record CustodianCalendarItem(Guid Id, string Name, string AssetCode, bool MaintenanceHold);
public sealed record CustodianCalendarDay(DateOnly Date, bool CurrentMonth, bool Today, bool Selected,
    string Status, int BookingCount);
public sealed record CustodianCalendarSlot(Guid Id, string BorrowerName, string StartManila,
    string EndManila, string Status);
public sealed record CustodianCalendarViewModel(IReadOnlyList<CustodianCalendarItem> Items,
    CustodianCalendarItem? SelectedItem, DateOnly Month, IReadOnlyList<CustodianCalendarDay> Days,
    DateOnly SelectedDay, IReadOnlyList<CustodianCalendarSlot> Slots, IReadOnlyList<EquipmentPickerItem> Picker);

public static class CustodianTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public static DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
    public static string Format(DateTime utc) => Local(utc).ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture);
    /// <summary>A loan may be returned at any time after release; one returned before its due time is an early return.</summary>
    public static bool IsReturnedEarly(Loan? loan) => loan is { ReturnedAtUtc: { } returned } && returned < loan.DueAtUtc;
    /// <summary>
    /// When the booking actually stops holding the item: the return time for an early return, otherwise the booked end.
    /// Calendars use this so returned equipment shows as free for the rest of the original booking.
    /// </summary>
    public static DateTime BookedUntil(Reservation reservation) =>
        reservation.Loan is { ReturnedAtUtc: { } returned } && returned < reservation.EndAtUtc ? returned : reservation.EndAtUtc;
    public static string Status(Reservation reservation, DateTime now)
    {
        if (reservation.Loan is { ReturnedAtUtc: null } loan && loan.DueAtUtc < now) return "Overdue";
        if (reservation.Loan is { ReturnedAtUtc: { } returned } completed && returned > completed.DueAtUtc) return "Returned late";
        if (IsReturnedEarly(reservation.Loan)) return "Returned early";
        if (reservation.Status == ReservationStatus.Pending && (reservation.HoldExpiresAtUtc == null || reservation.HoldExpiresAtUtc <= now || reservation.StartAtUtc <= now)) return "Expired";
        return reservation.Status switch { ReservationStatus.Released => "Borrowed", ReservationStatus.Completed => "Returned", _ => reservation.Status.ToString() };
    }
    private static TimeZoneInfo ResolveZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
    }
}
