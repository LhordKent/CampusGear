using System.Globalization;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

// =====================================================================
// AVAILABILITY CALENDAR SERVICE  -  the month calendars (read-only).
// Students see when equipment is free; staff see every booking.
// =====================================================================
namespace ASI.Basecode.Services.Services
{
    public sealed class AvailabilityCalendarService : ServiceBase, IAvailabilityCalendarService
    {
        private readonly IBorrowerReadRepository _reads;
        private readonly IEquipmentAvailabilityService _availability;
        private readonly TimeProvider _timeProvider;
        private readonly IEquipmentAvailabilityRepository _calendarReads;

        public AvailabilityCalendarService(
            IEquipmentAvailabilityRepository calendarReads,
            IBorrowerReadRepository reads,
            IEquipmentAvailabilityService availability,
            TimeProvider clock
        )
        {
            _reads = reads;
            _calendarReads = calendarReads;
            _availability = availability;
            _timeProvider = clock;
        }

        // The student's calendar: which days an item (or all items) is booked.
        public async Task<OperationResult<CalendarPageViewModel>> CalendarAsync(
            RequestContext actor,
            int? year,
            int? month,
            Guid? equipmentItemId,
            string? day,
            CancellationToken cancellationToken
        )
        {
            var profile = await OwnProfileAsync(actor, cancellationToken);
            if (profile is null)
            {
                return new(OperationOutcome.Forbidden);
            }

            var localToday = DateOnly.FromDateTime(ManilaTime.Local(_timeProvider.GetUtcNow().UtcDateTime));
            var chosenYear = year is >= 2000 and <= 2100 ? year.Value : localToday.Year;
            var chosenMonth = month is >= 1 and <= 12 ? month.Value : localToday.Month;
            var first = new DateOnly(chosenYear, chosenMonth, 1);
            var gridStart = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
            var gridEnd = gridStart.AddDays(42);
            var gridStartUtc = ManilaTime.Utc(gridStart.ToDateTime(TimeOnly.MinValue));
            var gridEndUtc = ManilaTime.Utc(gridEnd.ToDateTime(TimeOnly.MinValue));
            var items = (await _calendarReads.CalendarItemsAsync(cancellationToken))
                .Select(x => new CalendarEquipment(x.Id, x.Name, x.AssetCode, x.MaintenanceHold))
                .ToList();
            var item = items.FirstOrDefault(x => x.Id == equipmentItemId) ?? items.FirstOrDefault();
            var bookings = item is null
                ? new List<Reservation>()
                : (
                    await _calendarReads.CalendarBookingsAsync(
                        item.Id,
                        gridStartUtc,
                        gridEndUtc,
                        _timeProvider.GetUtcNow().UtcDateTime,
                        false,
                        cancellationToken
                    )
                ).ToList();
            var selected =
                ManilaTime.TryParseDate(day, out var parsedDay) && parsedDay >= gridStart && parsedDay < gridEnd ? parsedDay : localToday;
            if (selected < gridStart || selected >= gridEnd)
            {
                selected = first;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var days = new List<CalendarDay>(42);
            for (var date = gridStart; date < gridEnd; date = date.AddDays(1))
            {
                var dayStart = ManilaTime.Utc(date.ToDateTime(TimeOnly.MinValue));
                var dayEnd = ManilaTime.Utc(date.AddDays(1).ToDateTime(TimeOnly.MinValue));
                var overlapping = bookings
                    .Where(x =>
                        x.StartAtUtc < dayEnd
                        && ReservationDisplay.BookedUntil(x) > dayStart
                        && (x.Status != ReservationStatus.Pending || x.HoldExpiresAtUtc > now)
                    )
                    .ToList();
                var state = GetBorrowerDayStatus(item?.IsMaintenanceHold == true, overlapping);
                days.Add(
                    new CalendarDay(
                        Date: date,
                        IsCurrentMonth: date.Month == chosenMonth,
                        IsToday: date == localToday,
                        IsSelected: date == selected,
                        Status: state,
                        BookingCount: overlapping.Count
                    )
                );
            }

            var selectedStart = ManilaTime.Utc(selected.ToDateTime(TimeOnly.MinValue));
            var selectedEnd = ManilaTime.Utc(selected.AddDays(1).ToDateTime(TimeOnly.MinValue));
            var slots = bookings
                .Where(x =>
                    x.StartAtUtc < selectedEnd
                    && ReservationDisplay.BookedUntil(x) > selectedStart
                    && (x.Status != ReservationStatus.Pending || x.HoldExpiresAtUtc > now)
                )
                .OrderBy(x => x.StartAtUtc)
                .Select(x => new CalendarSlot(
                    ManilaTime.Local(x.StartAtUtc).ToString("h:mm tt", CultureInfo.InvariantCulture),
                    ManilaTime.Local(ReservationDisplay.BookedUntil(x)).ToString("h:mm tt", CultureInfo.InvariantCulture),
                    x.Status is ReservationStatus.Released or ReservationStatus.Completed ? "Borrowed" : "Reserved"
                ))
                .ToList();
            return OperationResult<CalendarPageViewModel>.Ok(
                new CalendarPageViewModel(
                    items,
                    item,
                    chosenYear,
                    chosenMonth,
                    first.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                    first.AddMonths(-1),
                    first.AddMonths(1),
                    days,
                    selected,
                    slots,
                    await _availability.GetPickerAsync(_timeProvider.GetUtcNow().UtcDateTime, cancellationToken)
                )
            );
        }

        private Task<BorrowerProfile?> OwnProfileAsync(RequestContext actor, CancellationToken ct)
        {
            return _reads.FindProfileAsync(actor.UserId, ct);
        }

        // The custodian/admin calendar: every booking in the month, with details for the chosen day.
        public async Task<CustodianCalendarViewModel> GetStaffCalendarAsync(
            int? year,
            int? month,
            Guid? equipmentItemId,
            string? day,
            CancellationToken cancellationToken
        )
        {
            var today = DateOnly.FromDateTime(ManilaTime.Local(_timeProvider.GetUtcNow().UtcDateTime));
            var first = new DateOnly(
                year is >= 2000 and <= 2100 ? year.Value : today.Year,
                month is >= 1 and <= 12 ? month.Value : today.Month,
                1
            );
            var gridStart = first.AddDays(-((int)first.DayOfWeek + 6) % 7);
            var gridEnd = gridStart.AddDays(42);
            var items = (await _calendarReads.CalendarItemsAsync(cancellationToken))
                .Select(x => new CustodianCalendarItem(x.Id, x.Name, x.AssetCode, x.MaintenanceHold))
                .ToList();
            var selectedItem = items.FirstOrDefault(x => x.Id == equipmentItemId) ?? items.FirstOrDefault();
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var startUtc = ManilaTime.Utc(gridStart.ToDateTime(TimeOnly.MinValue));
            var endUtc = ManilaTime.Utc(gridEnd.ToDateTime(TimeOnly.MinValue));
            var bookings = selectedItem is null
                ? []
                : await _calendarReads.CalendarBookingsAsync(selectedItem.Id, startUtc, endUtc, now, true, cancellationToken);
            var selectedDay =
                DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                && parsed >= gridStart
                && parsed < gridEnd
                    ? parsed
                    : today;
            if (selectedDay < gridStart || selectedDay >= gridEnd)
            {
                selectedDay = first;
            }

            var days = new List<CustodianCalendarDay>(42);
            for (var date = gridStart; date < gridEnd; date = date.AddDays(1))
            {
                var dateStart = ManilaTime.Utc(date.ToDateTime(TimeOnly.MinValue));
                var dateEnd = ManilaTime.Utc(date.AddDays(1).ToDateTime(TimeOnly.MinValue));
                var count = bookings.Count(x => x.StartAtUtc < dateEnd && ReservationDisplay.BookedUntil(x) > dateStart);
                days.Add(
                    new(
                        Date: date,
                        CurrentMonth: date.Month == first.Month,
                        Today: date == today,
                        Selected: date == selectedDay,
                        Status: GetStaffDayStatus(selectedItem?.MaintenanceHold == true, count),
                        BookingCount: count
                    )
                );
            }

            var selectedStart = ManilaTime.Utc(selectedDay.ToDateTime(TimeOnly.MinValue));
            var selectedEnd = ManilaTime.Utc(selectedDay.AddDays(1).ToDateTime(TimeOnly.MinValue));
            var slots = bookings
                .Where(x => x.StartAtUtc < selectedEnd && ReservationDisplay.BookedUntil(x) > selectedStart)
                .Select(x => new CustodianCalendarSlot(
                    x.Id,
                    x.BorrowerProfile.User.FullName,
                    ManilaTime.Format(x.StartAtUtc),
                    ManilaTime.Format(x.EndAtUtc),
                    ReservationDisplay.Status(x, now)
                ))
                .ToList();
            return new CustodianCalendarViewModel(
                items,
                selectedItem,
                first,
                days,
                selectedDay,
                slots,
                await _availability.GetPickerAsync(_timeProvider.GetUtcNow().UtcDateTime, cancellationToken)
            );
        }

        private static string GetBorrowerDayStatus(bool maintenanceHold, IReadOnlyList<Reservation> bookings)
        {
            if (maintenanceHold)
            {
                return "Maintenance";
            }

            // A released or completed loan occupies its actual interval, including an early return.
            if (bookings.Any(booking => booking.Status is ReservationStatus.Released or ReservationStatus.Completed))
            {
                return "Borrowed";
            }
            return bookings.Count > 0 ? "Reserved" : "Available";
        }

        private static string GetStaffDayStatus(bool maintenanceHold, int bookingCount)
        {
            if (maintenanceHold)
            {
                return "Maintenance";
            }
            return bookingCount > 0 ? "Reserved" : "Available";
        }
    }
}
