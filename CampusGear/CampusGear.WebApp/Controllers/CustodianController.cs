using System.Globalization;
using CampusGear.Data;
using CampusGear.Models;
using CampusGear.Services.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Controllers;

[Authorize(Roles = "Custodian,Administrator")]
[AutoValidateAntiforgeryToken]
public sealed class CustodianController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    IReservationService reservations,
    TimeProvider clock) : Controller
{
    private const int PageSize = 12;

    [HttpGet("custodian/approvals")]
    public async Task<IActionResult> Approvals(string? q, string? status, string? from, string? to,
        int page = 1, CancellationToken cancellationToken = default)
    {
        Workspace();
        var now = clock.GetUtcNow().UtcDateTime;
        status = status?.ToLowerInvariant() is "approved" or "all" ? status.ToLowerInvariant() : "pending";
        var query = db.Reservations.AsNoTracking().Where(x =>
            x.Status == ReservationStatus.Approved ||
            x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now);
        if (status == "pending") query = query.Where(x => x.Status == ReservationStatus.Pending);
        if (status == "approved") query = query.Where(x => x.Status == ReservationStatus.Approved);
        var model = await ListAsync(query, User.IsInRole("Administrator") ? nameof(AdminApprovals) : nameof(Approvals), "Reservation approvals",
            "Review pending requests, approve or reject them, and open approved bookings for release.",
            q, status, from, to, page, "approvals", cancellationToken);
        return View("Queue", model);
    }

    [HttpGet("custodian/returns")]
    public async Task<IActionResult> Returns(string? q, string? queue, string? from, string? to,
        int page = 1, CancellationToken cancellationToken = default)
    {
        Workspace();
        queue = queue?.ToLowerInvariant() == "release" ? "release" : "active";
        var query = db.Reservations.AsNoTracking().Where(x => queue == "release"
            ? x.Status == ReservationStatus.Approved
            : x.Status == ReservationStatus.Released && x.Loan != null && x.Loan.ReturnedAtUtc == null);
        var model = await ListAsync(query, User.IsInRole("Administrator") ? nameof(AdminReturns) : nameof(Returns), "Release and return desk",
            "Record equipment handover during the approved booking time, then inspect and record each return.",
            q, "all", from, to, page, queue, cancellationToken);
        return View("Queue", model);
    }

    [HttpGet("custodian/history")]
    public async Task<IActionResult> History(string? q, string? status, string? from, string? to,
        int page = 1, CancellationToken cancellationToken = default)
    {
        Workspace();
        status = status?.ToLowerInvariant() ?? "all";
        var now = clock.GetUtcNow().UtcDateTime;
        var query = db.Reservations.AsNoTracking();
        query = status switch
        {
            "pending" => query.Where(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now),
            "approved" => query.Where(x => x.Status == ReservationStatus.Approved),
            "borrowed" => query.Where(x => x.Status == ReservationStatus.Released),
            "returned" => query.Where(x => x.Status == ReservationStatus.Completed),
            "rejected" => query.Where(x => x.Status == ReservationStatus.Rejected),
            "cancelled" => query.Where(x => x.Status == ReservationStatus.Cancelled),
            "expired" => query.Where(x => x.Status == ReservationStatus.Expired || x.Status == ReservationStatus.Pending && (x.HoldExpiresAtUtc == null || x.HoldExpiresAtUtc <= now || x.StartAtUtc <= now)),
            "late" => query.Where(x => x.Loan != null && (x.Loan.ReturnedAtUtc == null && x.Loan.DueAtUtc < now || x.Loan.ReturnedAtUtc > x.Loan.DueAtUtc)),
            _ => query
        };
        if (status is not ("pending" or "approved" or "borrowed" or "returned" or "rejected" or "cancelled" or "expired" or "late")) status = "all";
        return View("Queue", await ListAsync(query, User.IsInRole("Administrator") ? nameof(AdminHistory) : nameof(History), "Borrowing history",
            "Track decisions, release records, return condition, and late equipment across all borrowers.",
            q, status, from, to, page, "history", cancellationToken));
    }

    [HttpGet("custodian/reservations/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Workspace();
        var record = await db.Reservations.AsNoTracking()
            .Include(x => x.BorrowerProfile).ThenInclude(x => x.User)
            .Include(x => x.EquipmentItem).ThenInclude(x => x.Category)
            .Include(x => x.DecidedByUser)
            .Include(x => x.Loan).ThenInclude(x => x!.ReleasedByUser)
            .Include(x => x.Loan).ThenInclude(x => x!.ReceivedByUser)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null) return NotFound();
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
        return View(new CustodianDetailViewModel(record, CustodianTime.Status(record, now),
            pending && usable, pending, canRelease,
            record.Status == ReservationStatus.Released && record.Loan is { ReturnedAtUtc: null },
            note, TempData["CustodianFeedback"] as string, TempData["CustodianError"] as string));
    }

    [HttpPost("custodian/reservations/{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, actor => reservations.ApproveAsync(actor, id, cancellationToken),
            "Reservation approved. The booking is ready for release at its start time.");

    [HttpPost("custodian/reservations/{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, [FromForm] string? reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            return InvalidFormAsync(id, "Enter a rejection reason using 1 to 1000 characters.");
        return TransitionAsync(id, actor => reservations.RejectAsync(actor, id, reason.Trim(), cancellationToken),
            "Request rejected. Its equipment slot is available again.");
    }

    [HttpPost("custodian/reservations/{id:guid}/release")]
    public Task<IActionResult> Release(Guid id, [FromForm] string? condition, [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        if (!ValidCondition(condition, out var parsed) || parsed is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable)
            return InvalidFormAsync(id, "Choose Excellent, Good, or Fair for an equipment release.");
        if (notes?.Length > 2000) return InvalidFormAsync(id, "Release notes must use 2000 characters or fewer.");
        return TransitionAsync(id, actor => reservations.ReleaseAsync(actor, id, parsed, notes, cancellationToken),
            "Release recorded. The equipment is now on loan.");
    }

    [HttpPost("custodian/reservations/{id:guid}/return")]
    public Task<IActionResult> RecordReturn(Guid id, [FromForm] string? condition, [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        if (!ValidCondition(condition, out var parsed)) return InvalidFormAsync(id, "Choose a valid return condition.");
        if (notes?.Length > 2000) return InvalidFormAsync(id, "Return notes must use 2000 characters or fewer.");
        return TransitionAsync(id, actor => reservations.ReturnAsync(actor, id, parsed, notes, cancellationToken),
            "Return recorded. The equipment condition and borrowing history are updated.", returnTransition: true);
    }

    [HttpGet("custodian/calendar")]
    public async Task<IActionResult> Calendar(int? year, int? month, Guid? equipmentItemId, string? day,
        CancellationToken cancellationToken)
    {
        Workspace();
        var today = DateOnly.FromDateTime(CustodianTime.Local(clock.GetUtcNow().UtcDateTime));
        var first = new DateOnly(year is >= 2000 and <= 2100 ? year.Value : today.Year,
            month is >= 1 and <= 12 ? month.Value : today.Month, 1);
        var gridStart = first.AddDays(-((int)first.DayOfWeek + 6) % 7);
        var gridEnd = gridStart.AddDays(42);
        var items = await db.EquipmentItems.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name).ThenBy(x => x.AssetCode)
            .Select(x => new CustodianCalendarItem(x.Id, x.Name, x.AssetCode,
                x.IsMaintenanceHold || x.Condition == EquipmentCondition.Damaged || x.Condition == EquipmentCondition.Unserviceable))
            .ToListAsync(cancellationToken);
        var selectedItem = items.FirstOrDefault(x => x.Id == equipmentItemId) ?? items.FirstOrDefault();
        var now = clock.GetUtcNow().UtcDateTime;
        var startUtc = CustodianTime.Utc(gridStart.ToDateTime(TimeOnly.MinValue));
        var endUtc = CustodianTime.Utc(gridEnd.ToDateTime(TimeOnly.MinValue));
        var bookings = selectedItem is null ? [] : await db.Reservations.AsNoTracking()
            .Include(x => x.BorrowerProfile).ThenInclude(x => x.User)
            .Where(x => x.EquipmentItemId == selectedItem.Id && x.StartAtUtc < endUtc && x.EndAtUtc > startUtc &&
                (x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Released || x.Status == ReservationStatus.Completed ||
                 x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now))
            .OrderBy(x => x.StartAtUtc).ToListAsync(cancellationToken);
        var selectedDay = DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) && parsed >= gridStart && parsed < gridEnd ? parsed : today;
        if (selectedDay < gridStart || selectedDay >= gridEnd) selectedDay = first;
        var days = new List<CustodianCalendarDay>(42);
        for (var date = gridStart; date < gridEnd; date = date.AddDays(1))
        {
            var dateStart = CustodianTime.Utc(date.ToDateTime(TimeOnly.MinValue));
            var dateEnd = CustodianTime.Utc(date.AddDays(1).ToDateTime(TimeOnly.MinValue));
            var count = bookings.Count(x => x.StartAtUtc < dateEnd && x.EndAtUtc > dateStart);
            days.Add(new(date, date.Month == first.Month, date == today, date == selectedDay,
                selectedItem?.MaintenanceHold == true ? "Maintenance" : count > 0 ? "Reserved" : "Available", count));
        }
        var selectedStart = CustodianTime.Utc(selectedDay.ToDateTime(TimeOnly.MinValue));
        var selectedEnd = CustodianTime.Utc(selectedDay.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var slots = bookings.Where(x => x.StartAtUtc < selectedEnd && x.EndAtUtc > selectedStart)
            .Select(x => new CustodianCalendarSlot(x.Id, x.BorrowerProfile.User.FullName,
                CustodianTime.Format(x.StartAtUtc), CustodianTime.Format(x.EndAtUtc), CustodianTime.Status(x, now))).ToList();
        return View("Calendar", new CustodianCalendarViewModel(items, selectedItem, first, days, selectedDay, slots));
    }

    [Authorize(Roles = "Administrator")]
    [HttpGet("admin/approvals")]
    public Task<IActionResult> AdminApprovals(string? q, string? status, string? from, string? to,
        int page = 1, CancellationToken cancellationToken = default) => Approvals(q, status, from, to, page, cancellationToken);

    [Authorize(Roles = "Administrator")]
    [HttpGet("admin/returns")]
    public Task<IActionResult> AdminReturns(string? q, string? queue, string? from, string? to,
        int page = 1, CancellationToken cancellationToken = default) => Returns(q, queue, from, to, page, cancellationToken);

    [Authorize(Roles = "Administrator")]
    [HttpGet("admin/history")]
    public Task<IActionResult> AdminHistory(string? q, string? status, string? from, string? to,
        int page = 1, CancellationToken cancellationToken = default) => History(q, status, from, to, page, cancellationToken);

    [Authorize(Roles = "Administrator")]
    [HttpGet("admin/calendar")]
    public Task<IActionResult> AdminCalendar(int? year, int? month, Guid? equipmentItemId, string? day,
        CancellationToken cancellationToken) => Calendar(year, month, equipmentItemId, day, cancellationToken);

    private async Task<CustodianListViewModel> ListAsync(IQueryable<Reservation> query, string action,
        string title, string intro, string? search, string status, string? from, string? to,
        int page, string queue, CancellationToken cancellationToken)
    {
        search = search?.Trim();
        if (search?.Length > 100) search = search[..100];
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x =>
            x.EquipmentItem.Name.Contains(search) || x.EquipmentItem.AssetCode.Contains(search) ||
            x.BorrowerProfile.User.FullName.Contains(search) || x.BorrowerProfile.User.Email!.Contains(search));
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
                if (fromDate is { } begin) query = query.Where(x => x.StartAtUtc >= CustodianTime.Utc(begin.ToDateTime(TimeOnly.MinValue)));
                if (toDate is { } end) query = query.Where(x => x.StartAtUtc < CustodianTime.Utc(end.AddDays(1).ToDateTime(TimeOnly.MinValue)));
            }
        }
        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        var included = query.Include(x => x.BorrowerProfile).ThenInclude(x => x.User)
            .Include(x => x.EquipmentItem).Include(x => x.Loan);
        var ordered = action is nameof(History) or nameof(AdminHistory)
            ? included.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            : included.OrderBy(x => x.StartAtUtc).ThenBy(x => x.CreatedAtUtc);
        var records = await ordered
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = records.Select(x => new CustodianListRow(x.Id, x.BorrowerProfile.User.FullName,
            x.BorrowerProfile.User.Email ?? string.Empty, x.EquipmentItem.Name, x.EquipmentItem.AssetCode,
            CustodianTime.Format(x.StartAtUtc), CustodianTime.Format(x.EndAtUtc),
            CustodianTime.Status(x, now), x.Loan?.ConditionAtReturn?.ToString(),
            x.Loan?.ReturnedAtUtc is { } returned ? CustodianTime.Format(returned) : null)).ToList();
        var approved = await db.Reservations.CountAsync(x => x.Status == ReservationStatus.Approved, cancellationToken);
        var active = await db.Loans.CountAsync(x => x.ReturnedAtUtc == null, cancellationToken);
        var pending = await db.Reservations.CountAsync(x => x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now, cancellationToken);
        return new(title, intro, action, search ?? string.Empty, status, from ?? string.Empty, to ?? string.Empty,
            queue, page, pages, total, rows, pending, approved, active,
            TempData["CustodianFeedback"] as string, filterError ?? TempData["CustodianError"] as string);
    }

    private async Task<IActionResult> TransitionAsync(Guid id,
        Func<ReservationActor, Task<ReservationResult>> operation, string success, bool returnTransition = false)
    {
        var userId = users.GetUserId(User);
        if (userId is null) return Forbid();
        try
        {
            var result = await operation(new(userId, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier));
            TempData["CustodianFeedback"] = result.Status == ReservationStatus.Expired
                ? "The pending hold expired before this action. Its equipment slot was released."
                : returnTransition && result.ConditionAtReturn is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable
                    ? "Return recorded. A maintenance hold was created for this equipment."
                    : success;
        }
        catch (ReservationWorkflowException exception)
        {
            if (exception.Code == ReservationErrorCode.Forbidden) return Forbid();
            if (exception.Code == ReservationErrorCode.NotFound) return NotFound();
            TempData["CustodianError"] = exception.Message;
        }
        return Redirect($"/custodian/reservations/{id:D}");
    }

    private Task<IActionResult> InvalidFormAsync(Guid id, string error)
    {
        TempData["CustodianError"] = error;
        return Task.FromResult<IActionResult>(Redirect($"/custodian/reservations/{id:D}"));
    }

    private static bool ValidCondition(string? value, out EquipmentCondition condition) =>
        Enum.TryParse(value, true, out condition) && Enum.IsDefined(condition);

    private void Workspace() => ViewData["WorkspaceRole"] = User.IsInRole("Administrator") ? "Administrator" : "Custodian";
}

public sealed record CustodianListRow(Guid Id, string BorrowerName, string BorrowerEmail,
    string EquipmentName, string AssetCode, string StartManila, string EndManila,
    string Status, string? ReturnCondition, string? ReturnedManila);
public sealed record CustodianListViewModel(string Title, string Intro, string ActionName,
    string Search, string Status, string From, string To, string Queue, int Page, int PageCount,
    int Total, IReadOnlyList<CustodianListRow> Rows, int PendingCount, int ApprovedCount, int ActiveCount,
    string? Feedback, string? Error);
public sealed record CustodianDetailViewModel(Reservation Record, string Status, bool CanApprove,
    bool CanReject, bool CanRelease, bool CanReturn, string? AvailabilityNote, string? Feedback, string? Error);
public sealed record CustodianCalendarItem(Guid Id, string Name, string AssetCode, bool MaintenanceHold);
public sealed record CustodianCalendarDay(DateOnly Date, bool CurrentMonth, bool Today, bool Selected,
    string Status, int BookingCount);
public sealed record CustodianCalendarSlot(Guid Id, string BorrowerName, string StartManila,
    string EndManila, string Status);
public sealed record CustodianCalendarViewModel(IReadOnlyList<CustodianCalendarItem> Items,
    CustodianCalendarItem? SelectedItem, DateOnly Month, IReadOnlyList<CustodianCalendarDay> Days,
    DateOnly SelectedDay, IReadOnlyList<CustodianCalendarSlot> Slots);

public static class CustodianTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public static DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
    public static string Format(DateTime utc) => Local(utc).ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture);
    public static string Status(Reservation reservation, DateTime now)
    {
        if (reservation.Loan is { ReturnedAtUtc: null } loan && loan.DueAtUtc < now) return "Overdue";
        if (reservation.Loan is { ReturnedAtUtc: { } returned } completed && returned > completed.DueAtUtc) return "Returned late";
        if (reservation.Status == ReservationStatus.Pending && (reservation.HoldExpiresAtUtc == null || reservation.HoldExpiresAtUtc <= now || reservation.StartAtUtc <= now)) return "Expired";
        return reservation.Status switch { ReservationStatus.Released => "Borrowed", ReservationStatus.Completed => "Returned", _ => reservation.Status.ToString() };
    }
    private static TimeZoneInfo ResolveZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
    }
}
