using System.Globalization;
using CampusGear.Controllers;
using CampusGear.Data.Interfaces;
using CampusGear.Data.QueryModels;
using CampusGear.Models;
namespace CampusGear.Services;
public sealed class CustodianService(ICustodianReadRepository reads, IBorrowerReadRepository borrowerReads,
    IEquipmentAvailabilityService availability, TimeProvider clock) : ICustodianService
{
    public async Task<CustodianDetailViewModel?> GetDetailsAsync(Guid id, bool custodian, CancellationToken cancellationToken)
    {
        var record = await reads.DetailsAsync(id, cancellationToken);
        if (record is null) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var pending = record.Status == ReservationStatus.Pending && record.HoldExpiresAtUtc > now && record.StartAtUtc > now;
        var usable = record.EquipmentItem.IsActive && !record.EquipmentItem.IsMaintenanceHold &&
            record.EquipmentItem.Condition is not (EquipmentCondition.Damaged or EquipmentCondition.Unserviceable);
        var canRelease = record.Status == ReservationStatus.Approved && usable && now >= record.StartAtUtc && now < record.EndAtUtc;
        var note = !usable ? "This item is inactive or on a maintenance hold. It cannot be approved or released."
            : record.Status == ReservationStatus.Approved && now < record.StartAtUtc ? "Release opens at the approved start time."
            : record.Status == ReservationStatus.Approved && now >= record.EndAtUtc ? "The booking time has ended. Equipment can no longer be released."
            : record.Status == ReservationStatus.Pending && !pending ? "The pending hold expired. This request cannot be approved or rejected."
            : null;
        // Administrators see the record read-only: every action below is offered to custodians only.
        return new CustodianDetailViewModel(ReservationRecord.From(record), CustodianTime.Status(record, now),
            custodian && pending && usable, custodian && pending, custodian && canRelease,
            custodian && record.Status == ReservationStatus.Released && record.Loan is { ReturnedAtUtc: null },
            note, null, null);
    }
    public async Task<CustodianCalendarViewModel> GetCalendarAsync(int? year, int? month, Guid? equipmentItemId, string? day,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(CustodianTime.Local(clock.GetUtcNow().UtcDateTime));
        var first = new DateOnly(year is >= 2000 and <= 2100 ? year.Value : today.Year,
            month is >= 1 and <= 12 ? month.Value : today.Month, 1);
        var gridStart = first.AddDays(-((int)first.DayOfWeek + 6) % 7);
        var gridEnd = gridStart.AddDays(42);
        var items = (await borrowerReads.CalendarItemsAsync(cancellationToken)).Select(x => new CustodianCalendarItem(x.Id, x.Name, x.AssetCode, x.MaintenanceHold)).ToList();
        var selectedItem = items.FirstOrDefault(x => x.Id == equipmentItemId) ?? items.FirstOrDefault();
        var now = clock.GetUtcNow().UtcDateTime;
        var startUtc = CustodianTime.Utc(gridStart.ToDateTime(TimeOnly.MinValue));
        var endUtc = CustodianTime.Utc(gridEnd.ToDateTime(TimeOnly.MinValue));
        var bookings = selectedItem is null ? [] : await borrowerReads.CalendarBookingsAsync(selectedItem.Id, startUtc, endUtc, now, true, cancellationToken);
        var selectedDay = DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) && parsed >= gridStart && parsed < gridEnd ? parsed : today;
        if (selectedDay < gridStart || selectedDay >= gridEnd) selectedDay = first;
        var days = new List<CustodianCalendarDay>(42);
        for (var date = gridStart; date < gridEnd; date = date.AddDays(1))
        {
            var dateStart = CustodianTime.Utc(date.ToDateTime(TimeOnly.MinValue));
            var dateEnd = CustodianTime.Utc(date.AddDays(1).ToDateTime(TimeOnly.MinValue));
            var count = bookings.Count(x => x.StartAtUtc < dateEnd && CustodianTime.BookedUntil(x) > dateStart);
            days.Add(new(date, date.Month == first.Month, date == today, date == selectedDay,
                selectedItem?.MaintenanceHold == true ? "Maintenance" : count > 0 ? "Reserved" : "Available", count));
        }
        var selectedStart = CustodianTime.Utc(selectedDay.ToDateTime(TimeOnly.MinValue));
        var selectedEnd = CustodianTime.Utc(selectedDay.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var slots = bookings.Where(x => x.StartAtUtc < selectedEnd && CustodianTime.BookedUntil(x) > selectedStart)
            .Select(x => new CustodianCalendarSlot(x.Id, x.BorrowerProfile.User.FullName,
                CustodianTime.Format(x.StartAtUtc), CustodianTime.Format(x.EndAtUtc), CustodianTime.Status(x, now))).ToList();
        return new CustodianCalendarViewModel(items, selectedItem, first, days, selectedDay, slots,
            await availability.GetPickerAsync(clock.GetUtcNow().UtcDateTime, cancellationToken));
    }
    public async Task<CustodianListViewModel> GetListAsync(string action, string title, string? search, string? status, string? from, string? to,
        int page, string queue, CancellationToken cancellationToken)
    {
        search = search?.Trim();
        if (search?.Length > 100) search = search[..100];
        status = status?.ToLowerInvariant() ?? "all";
        if (queue == "approvals") status = status is "approved" or "all" ? status : "pending";
        else if (status is not ("pending" or "approved" or "borrowed" or "returned" or "rejected" or "cancelled" or "expired" or "late")) status = "all";
        DateTime? beginUtc = null, endUtc = null;
        string? filterError = null;
        var fromValid = string.IsNullOrWhiteSpace(from) || DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        var toValid = string.IsNullOrWhiteSpace(to) || DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        if (!fromValid || !toValid) filterError = "Use valid dates for the date filters.";
        else
        {
            DateOnly? fromDate = string.IsNullOrWhiteSpace(from) ? null : DateOnly.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            DateOnly? toDate = string.IsNullOrWhiteSpace(to) ? null : DateOnly.ParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (fromDate is { Year: < 2000 or > 2100 } || toDate is { Year: < 2000 or > 2100 })
                filterError = "Choose filter dates between 2000 and 2100.";
            else if (fromDate > toDate) filterError = "The end date must be on or after the start date.";
            else
            {
                if (fromDate is { } begin) beginUtc = CustodianTime.Utc(begin.ToDateTime(TimeOnly.MinValue));
                if (toDate is { } end) endUtc = CustodianTime.Utc(end.AddDays(1).ToDateTime(TimeOnly.MinValue));
            }
        }
        var now = clock.GetUtcNow().UtcDateTime;
        var result = await reads.ListAsync(queue, status, search, beginUtc, endUtc, now, page, cancellationToken);
        var records = result.Rows; var total = result.Total; var pages = result.Pages; page = result.Page;
        var rows = records.Select(x => new CustodianListRow(x.Id, x.BorrowerProfile.User.FullName,
            x.BorrowerProfile.User.Email ?? string.Empty, x.EquipmentItem.Name, x.EquipmentItem.AssetCode,
            CustodianTime.Format(x.StartAtUtc), CustodianTime.Format(x.EndAtUtc),
            CustodianTime.Status(x, now), x.Loan?.ConditionAtReturn?.ToString(),
            x.Loan?.ReturnedAtUtc is { } returned ? CustodianTime.Format(returned) : null)).ToList();
        return new(title, action, search ?? string.Empty, status, from ?? string.Empty, to ?? string.Empty,
            queue, page, pages, total, rows, result.Pending, result.Approved, result.Active,
            null, filterError);
    }
}
