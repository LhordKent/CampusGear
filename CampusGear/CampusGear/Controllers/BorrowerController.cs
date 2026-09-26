using System.Globalization;
using CampusGear.Data;
using CampusGear.Models;
using CampusGear.Services.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Controllers;

[Authorize(Roles = "Borrower")]
public sealed class BorrowerController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    IReservationService reservations,
    TimeProvider clock) : Controller
{
    private static readonly TimeZoneInfo ManilaZone = ResolveManilaZone();
    private const int HistoryPageSize = 12;

    [HttpGet("borrower/reservation")]
    public async Task<IActionResult> Reservation(
        Guid? equipmentItemId, string? state, Guid? id, CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(cancellationToken);
        if (profile is null) return Forbid();

        var localNow = Manila(clock.GetUtcNow().UtcDateTime);
        var start = new DateTime(localNow.Year, localNow.Month, localNow.Day,
            localNow.Hour, 0, 0).AddHours(1);
        var input = new ReservationInputModel
        {
            RequestId = Guid.NewGuid(),
            EquipmentItemId = equipmentItemId ?? Guid.Empty,
            StartLocal = LocalInput(start),
            EndLocal = LocalInput(start.AddHours(2))
        };
        var model = await BuildReservationPageAsync(profile, input, cancellationToken);
        if (input.EquipmentItemId == Guid.Empty && model.Items.Count > 0)
            input.EquipmentItemId = model.Items[0].Id;

        // A query string is never proof of submission. Confirm the persisted record belongs to this borrower.
        if (state == "submitted" && id is not null)
        {
            model.Submitted = await db.Reservations.AsNoTracking()
                .Include(x => x.EquipmentItem)
                .FirstOrDefaultAsync(x => x.Id == id && x.BorrowerProfileId == profile.Id,
                    cancellationToken) is { } submitted
                ? new SubmittedReservation(
                    submitted.Id, submitted.EquipmentItem.Name, submitted.EquipmentItem.AssetCode,
                    FormatManila(submitted.StartAtUtc), FormatManila(submitted.EndAtUtc),
                    submitted.HoldExpiresAtUtc is { } hold ? FormatManila(hold) : null,
                    submitted.Status)
                : null;
        }

        return View(model);
    }

    [HttpPost("borrower/reservation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReservation(
        ReservationInputModel input, CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(cancellationToken);
        if (profile is null) return Forbid();

        var model = await BuildReservationPageAsync(profile, input, cancellationToken);
        if (!profile.IsEligible)
            ModelState.AddModelError(string.Empty, "Your borrowing access is currently paused. Contact an administrator.");
        if (input.RequestId == Guid.Empty)
            ModelState.AddModelError(nameof(input.RequestId), "Reload the form and try again.");
        if (!model.Items.Any(x => x.Id == input.EquipmentItemId))
            ModelState.AddModelError(nameof(input.EquipmentItemId), "Choose available equipment.");
        if (string.IsNullOrWhiteSpace(input.Purpose))
            ModelState.AddModelError(nameof(input.Purpose), "Enter the purpose of your request.");
        else if (input.Purpose.Trim().Length > 500)
            ModelState.AddModelError(nameof(input.Purpose), "Use 500 characters or fewer.");
        if (input.Notes?.Trim().Length > 2000)
            ModelState.AddModelError(nameof(input.Notes), "Use 2000 characters or fewer.");

        var startUtc = ParseManilaInput(input.StartLocal, nameof(input.StartLocal));
        var endUtc = ParseManilaInput(input.EndLocal, nameof(input.EndLocal));
        if (startUtc is not null && endUtc is not null && startUtc >= endUtc)
            ModelState.AddModelError(nameof(input.EndLocal), "The end time must be after the start time.");
        if (startUtc is not null && startUtc <= clock.GetUtcNow().UtcDateTime)
            ModelState.AddModelError(nameof(input.StartLocal), "Choose a future start time.");

        if (!ModelState.IsValid)
            return View("Reservation", model);

        try
        {
            var result = await reservations.CreateAsync(
                Actor(),
                new CreateReservationCommand(input.RequestId, profile.UserId,
                    input.EquipmentItemId, startUtc!.Value, endUtc!.Value,
                    input.Purpose!.Trim(), input.Notes), cancellationToken);
            return Redirect($"/borrower/reservation?equipmentItemId={result.EquipmentItemId:D}&state=submitted&id={result.Id:D}");
        }
        catch (ReservationWorkflowException exception)
        {
            model.Feedback = exception.Message;
            model.IsConflict = exception.Code == ReservationErrorCode.ScheduleConflict;
            if (exception.Code == ReservationErrorCode.Forbidden)
                return Forbid();
            return View("Reservation", model);
        }
    }

    [HttpPost("borrower/reservation/{id:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await reservations.CancelAsync(Actor(), id, cancellationToken: cancellationToken);
            TempData["BorrowerFeedback"] = result.Status == ReservationStatus.Expired
                ? "The pending hold had already expired."
                : "Reservation cancelled. The equipment slot is available again.";
        }
        catch (ReservationWorkflowException exception)
        {
            if (exception.Code is ReservationErrorCode.Forbidden or ReservationErrorCode.NotFound)
                return NotFound();
            TempData["BorrowerFeedback"] = exception.Message;
        }
        return Redirect("/borrower/history");
    }

    [HttpGet("borrower/history")]
    public async Task<IActionResult> History(
        string? status, string? from, string? to, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var profile = await OwnProfileAsync(cancellationToken);
        if (profile is null) return Forbid();

        status = NormalizeStatus(status);
        var query = db.Reservations.AsNoTracking()
            .Where(x => x.BorrowerProfileId == profile.Id);
        var now = clock.GetUtcNow().UtcDateTime;
        query = status switch
        {
            "pending" => query.Where(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now),
            "approved" => query.Where(x => x.Status == ReservationStatus.Approved),
            "borrowed" => query.Where(x => x.Status == ReservationStatus.Released),
            "returned" => query.Where(x => x.Status == ReservationStatus.Completed),
            "late" => query.Where(x => x.Loan != null &&
                (x.Loan.ReturnedAtUtc == null && x.Loan.DueAtUtc < now ||
                 x.Loan.ReturnedAtUtc > x.Loan.DueAtUtc)),
            "cancelled" => query.Where(x => x.Status == ReservationStatus.Cancelled),
            _ => query
        };

        if (TryParseDate(from, out var fromDate))
        {
            var fromUtc = ToUtc(fromDate.ToDateTime(TimeOnly.MinValue));
            query = query.Where(x => x.StartAtUtc >= fromUtc);
        }
        else if (!string.IsNullOrEmpty(from))
            ModelState.AddModelError("from", "Choose a valid from date.");
        if (TryParseDate(to, out var toDate) && toDate < DateOnly.MaxValue)
        {
            var toUtc = ToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(x => x.StartAtUtc < toUtc);
        }
        else if (!string.IsNullOrEmpty(to))
            ModelState.AddModelError("to", "Choose a valid to date.");
        if (TryParseDate(from, out fromDate) && TryParseDate(to, out toDate) && fromDate > toDate)
            ModelState.AddModelError("to", "The to date must be on or after the from date.");

        var count = await query.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(count / (double)HistoryPageSize));
        page = Math.Clamp(page, 1, pageCount);
        var records = await query.Include(x => x.EquipmentItem).Include(x => x.Loan)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * HistoryPageSize).Take(HistoryPageSize)
            .ToListAsync(cancellationToken);

        var rows = records.Select(x => new HistoryRow(
            x.Id, x.EquipmentItem.Name, x.EquipmentItem.AssetCode,
            FormatManila(x.StartAtUtc), FormatManila(x.EndAtUtc),
            HistoryStatus(x, now), x.Purpose,
            (x.Status is ReservationStatus.Pending or ReservationStatus.Approved) &&
                (x.Status != ReservationStatus.Pending || x.HoldExpiresAtUtc > now),
            x.Loan?.ReturnedAtUtc is { } returned ? FormatManila(returned) : null,
            x.Loan?.ConditionAtReturn?.ToString())).ToList();

        return View(new HistoryPageViewModel(
            rows, status, from ?? string.Empty, to ?? string.Empty,
            page, pageCount, count, TempData["BorrowerFeedback"] as string));
    }

    [HttpGet("borrower/calendar")]
    public async Task<IActionResult> Calendar(
        int? year, int? month, Guid? equipmentItemId, string? day,
        CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(cancellationToken);
        if (profile is null) return Forbid();

        var localToday = DateOnly.FromDateTime(Manila(clock.GetUtcNow().UtcDateTime));
        var chosenYear = year is >= 2000 and <= 2100 ? year.Value : localToday.Year;
        var chosenMonth = month is >= 1 and <= 12 ? month.Value : localToday.Month;
        var first = new DateOnly(chosenYear, chosenMonth, 1);
        var gridStart = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        var gridEnd = gridStart.AddDays(42);
        var gridStartUtc = ToUtc(gridStart.ToDateTime(TimeOnly.MinValue));
        var gridEndUtc = ToUtc(gridEnd.ToDateTime(TimeOnly.MinValue));

        var items = await db.EquipmentItems.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name).ThenBy(x => x.AssetCode)
            .Select(x => new CalendarEquipment(x.Id, x.Name, x.AssetCode,
                x.IsMaintenanceHold || x.Condition == EquipmentCondition.Damaged ||
                x.Condition == EquipmentCondition.Unserviceable))
            .ToListAsync(cancellationToken);
        var item = items.FirstOrDefault(x => x.Id == equipmentItemId) ?? items.FirstOrDefault();

        var bookings = item is null
            ? new List<Reservation>()
            : await db.Reservations.AsNoTracking()
                .Where(x => x.EquipmentItemId == item.Id &&
                    x.StartAtUtc < gridEndUtc && x.EndAtUtc > gridStartUtc &&
                    (x.Status == ReservationStatus.Pending ||
                     x.Status == ReservationStatus.Approved ||
                     x.Status == ReservationStatus.Released ||
                     x.Status == ReservationStatus.Completed))
                .ToListAsync(cancellationToken);

        var selected = TryParseDate(day, out var parsedDay) &&
            parsedDay >= gridStart && parsedDay < gridEnd ? parsedDay : localToday;
        if (selected < gridStart || selected >= gridEnd)
            selected = first;

        var now = clock.GetUtcNow().UtcDateTime;
        var days = new List<CalendarDay>(42);
        for (var date = gridStart; date < gridEnd; date = date.AddDays(1))
        {
            var dayStart = ToUtc(date.ToDateTime(TimeOnly.MinValue));
            var dayEnd = ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue));
            var overlapping = bookings.Where(x => x.StartAtUtc < dayEnd && x.EndAtUtc > dayStart &&
                (x.Status != ReservationStatus.Pending || x.HoldExpiresAtUtc > now)).ToList();
            var state = item?.IsMaintenanceHold == true ? "Maintenance"
                : overlapping.Any(x => x.Status is ReservationStatus.Released or ReservationStatus.Completed) ? "Borrowed"
                : overlapping.Count > 0 ? "Reserved" : "Available";
            days.Add(new CalendarDay(date, date.Month == chosenMonth, date == localToday,
                date == selected, state, overlapping.Count));
        }

        var selectedStart = ToUtc(selected.ToDateTime(TimeOnly.MinValue));
        var selectedEnd = ToUtc(selected.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var slots = bookings.Where(x => x.StartAtUtc < selectedEnd && x.EndAtUtc > selectedStart &&
                (x.Status != ReservationStatus.Pending || x.HoldExpiresAtUtc > now))
            .OrderBy(x => x.StartAtUtc)
            .Select(x => new CalendarSlot(
                Manila(x.StartAtUtc).ToString("h:mm tt", CultureInfo.InvariantCulture),
                Manila(x.EndAtUtc).ToString("h:mm tt", CultureInfo.InvariantCulture),
                x.Status is ReservationStatus.Released or ReservationStatus.Completed ? "Borrowed" : "Reserved"))
            .ToList();

        return View(new CalendarPageViewModel(
            items, item, chosenYear, chosenMonth, first.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            first.AddMonths(-1), first.AddMonths(1), days, selected, slots));
    }

    private async Task<BorrowerProfile?> OwnProfileAsync(CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User);
        return userId is null ? null : await db.BorrowerProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    private ReservationActor Actor() => new(
        users.GetUserId(User)!,
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        HttpContext.TraceIdentifier);

    private async Task<ReservationPageViewModel> BuildReservationPageAsync(
        BorrowerProfile profile, ReservationInputModel input, CancellationToken cancellationToken)
    {
        var items = await db.EquipmentItems.AsNoTracking()
            .Where(x => x.IsActive && !x.IsMaintenanceHold &&
                x.Condition != EquipmentCondition.Damaged &&
                x.Condition != EquipmentCondition.Unserviceable)
            .OrderBy(x => x.Name).ThenBy(x => x.AssetCode)
            .Select(x => new EquipmentChoice(x.Id, x.Name, x.AssetCode,
                x.Category.Name, x.Condition, x.Location))
            .ToListAsync(cancellationToken);
        return new ReservationPageViewModel(input, items, profile.IsEligible);
    }

    private DateTime? ParseManilaInput(string? value, string field)
    {
        if (!DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local) || local.Year is < 2000 or > 2100)
        {
            ModelState.AddModelError(field, "Choose a valid date and time in Manila time.");
            return null;
        }
        return ToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
    }

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date) && date.Year is >= 2000 and <= 2100;

    private static string NormalizeStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "pending" or "approved" or "borrowed" or "returned" or "late" or "cancelled" => status.ToLowerInvariant(),
        _ => "all"
    };

    private static string HistoryStatus(Reservation reservation, DateTime now)
    {
        if (reservation.Loan is { ReturnedAtUtc: null } loan && loan.DueAtUtc < now)
            return "Overdue";
        if (reservation.Loan is { ReturnedAtUtc: { } returned } completed &&
            returned > completed.DueAtUtc)
            return "Returned late";
        if (reservation.Status == ReservationStatus.Pending &&
            (reservation.HoldExpiresAtUtc == null || reservation.HoldExpiresAtUtc <= now || reservation.StartAtUtc <= now))
            return "Expired";
        return reservation.Status switch
        {
            ReservationStatus.Released => "Borrowed",
            ReservationStatus.Completed => "Returned",
            _ => reservation.Status.ToString()
        };
    }

    private static TimeZoneInfo ResolveManilaZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        }
    }

    private static DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(local, DateTimeKind.Unspecified), ManilaZone);

    private static DateTime Manila(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(utc, DateTimeKind.Utc), ManilaZone);

    private static string FormatManila(DateTime utc) =>
        Manila(utc).ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture);

    private static string LocalInput(DateTime local) =>
        local.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
}

public sealed class ReservationInputModel
{
    public Guid RequestId { get; set; }
    public Guid EquipmentItemId { get; set; }
    public string? StartLocal { get; set; }
    public string? EndLocal { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReservationPageViewModel(
    ReservationInputModel input, IReadOnlyList<EquipmentChoice> items, bool isEligible)
{
    public ReservationInputModel Input { get; } = input;
    public IReadOnlyList<EquipmentChoice> Items { get; } = items;
    public bool IsEligible { get; } = isEligible;
    public EquipmentChoice? Selected => Items.FirstOrDefault(x => x.Id == Input.EquipmentItemId);
    public SubmittedReservation? Submitted { get; set; }
    public string? Feedback { get; set; }
    public bool IsConflict { get; set; }
}

public sealed record EquipmentChoice(Guid Id, string Name, string AssetCode,
    string Category, EquipmentCondition Condition, string? Location);

public sealed record SubmittedReservation(Guid Id, string EquipmentName, string AssetCode,
    string StartManila, string EndManila, string? HoldExpiresManila, ReservationStatus Status);

public sealed record HistoryRow(Guid Id, string EquipmentName, string AssetCode,
    string StartManila, string EndManila, string Status, string Purpose,
    bool CanCancel, string? ReturnedManila, string? ReturnCondition);

public sealed record HistoryPageViewModel(IReadOnlyList<HistoryRow> Rows,
    string Status, string From, string To, int Page, int PageCount, int Total,
    string? Feedback);

public sealed record CalendarEquipment(Guid Id, string Name, string AssetCode, bool IsMaintenanceHold);
public sealed record CalendarDay(DateOnly Date, bool IsCurrentMonth, bool IsToday,
    bool IsSelected, string Status, int BookingCount);
public sealed record CalendarSlot(string StartManila, string EndManila, string Status);
public sealed record CalendarPageViewModel(
    IReadOnlyList<CalendarEquipment> Items, CalendarEquipment? SelectedItem,
    int Year, int Month, string MonthTitle, DateOnly PreviousMonth, DateOnly NextMonth,
    IReadOnlyList<CalendarDay> Days, DateOnly SelectedDate,
    IReadOnlyList<CalendarSlot> SelectedSlots);
