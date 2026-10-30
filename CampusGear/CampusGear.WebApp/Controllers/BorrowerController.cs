using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
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
    // Upper bound on units in one request; each unit becomes its own reservation for staff to approve.
    internal const int MaxUnitsPerRequest = 10;

    /// <summary>The borrower's homepage: a welcome, the newest equipment, and their own reservations at a glance.</summary>
    [HttpGet("borrower/home")]
    [HttpGet("borrower/dashboard")]
    public async Task<IActionResult> Home(CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(cancellationToken);
        if (profile is null) return Forbid();
        var user = await users.GetUserAsync(User);

        var now = clock.GetUtcNow().UtcDateTime;
        var catalog = await BuildReservationPageAsync(profile, new ReservationInputModel(), cancellationToken);
        var mine = db.Reservations.AsNoTracking().Where(x => x.BorrowerProfileId == profile.Id);
        var upcoming = await mine.Include(x => x.EquipmentItem).Include(x => x.Loan)
            .Where(x => x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Released ||
                x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now && x.StartAtUtc > now)
            .OrderBy(x => x.StartAtUtc).Take(4).ToListAsync(cancellationToken);

        return View(new BorrowerHomeViewModel(
            user?.FirstName is { Length: > 0 } first ? first : "there",
            profile.IsEligible,
            catalog.Items.OrderByDescending(x => x.AddedAtUtc).ThenBy(x => x.Name).Take(4).ToList(),
            catalog.Items.Where(x => x.FreeNowUnits > 0).OrderByDescending(x => x.FreeNowUnits).ThenBy(x => x.Name).Take(6).ToList(),
            catalog.Items.GroupBy(x => x.Category)
                .Select(x => new HomeCategory(x.Key, x.Count(), x.Sum(item => item.FreeNowUnits)))
                .OrderBy(x => x.Name).ToList(),
            catalog.Items.Count,
            catalog.Items.Sum(x => x.FreeNowUnits),
            upcoming.Select(x => new HomeReservation(x.Id, x.EquipmentItem.Name, x.EquipmentItem.AssetCode,
                FormatManila(x.StartAtUtc), FormatManila(x.EndAtUtc), HistoryStatus(x, now))).ToList(),
            await mine.CountAsync(x => x.Status == ReservationStatus.Pending &&
                x.HoldExpiresAtUtc > now && x.StartAtUtc > now, cancellationToken),
            await mine.CountAsync(x => x.Status == ReservationStatus.Approved, cancellationToken),
            await db.Loans.CountAsync(x => x.Reservation.BorrowerProfileId == profile.Id && x.ReturnedAtUtc == null, cancellationToken),
            await db.Loans.CountAsync(x => x.Reservation.BorrowerProfileId == profile.Id && x.ReturnedAtUtc == null &&
                x.DueAtUtc < now, cancellationToken),
            await mine.CountAsync(x => x.Status == ReservationStatus.Completed, cancellationToken)));
    }

    [HttpGet("borrower/reservation")]
    public async Task<IActionResult> Reservation(
        Guid? equipmentItemId, string? state, Guid[]? id, CancellationToken cancellationToken)
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

        // A query string is never proof of submission. Confirm the persisted records belong to this borrower.
        if (state == "submitted" && id is { Length: > 0 })
        {
            var ids = id.Distinct().Take(MaxUnitsPerRequest).ToList();
            var submitted = await db.Reservations.AsNoTracking()
                .Include(x => x.EquipmentItem)
                .Where(x => ids.Contains(x.Id) && x.BorrowerProfileId == profile.Id)
                .OrderBy(x => x.EquipmentItem.AssetCode)
                .ToListAsync(cancellationToken);
            model.Submitted = submitted.Select(x => new SubmittedReservation(
                x.Id, x.EquipmentItem.Name, x.EquipmentItem.AssetCode,
                FormatManila(x.StartAtUtc), FormatManila(x.EndAtUtc),
                x.HoldExpiresAtUtc is { } hold ? FormatManila(hold) : null,
                x.Status)).ToList();
        }
        // A deep link such as the calendar's "Reserve this equipment" opens that item's request dialog.
        model.OpenDialog = model.Submitted.Count == 0 && model.Selected is not null;

        return View(model);
    }

    [HttpGet("borrower/reservation/availability")]
    public async Task<IActionResult> Availability(
        Guid equipmentItemId, string? start, string? end, CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(cancellationToken);
        if (profile is null) return Forbid();

        var unit = await db.EquipmentItems.AsNoTracking()
            .Where(x => x.Id == equipmentItemId && x.IsActive)
            .Select(x => new { x.CategoryId, x.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (unit is null) return NotFound();
        if (!TryParseManila(start, out var startUtc) || !TryParseManila(end, out var endUtc))
            return BadRequest(new { message = "Choose a valid borrowing and return date and time." });
        if (startUtc >= endUtc)
            return BadRequest(new { message = "The return time must be after the borrowing time." });
        var now = clock.GetUtcNow().UtcDateTime;
        if (startUtc <= now)
            return BadRequest(new { message = "Choose a future borrowing time." });

        var bookable = await db.EquipmentItems.AsNoTracking()
            .Where(x => x.IsActive && x.CategoryId == unit.CategoryId && x.Name == unit.Name &&
                !x.IsMaintenanceHold &&
                x.Condition != EquipmentCondition.Damaged &&
                x.Condition != EquipmentCondition.Unserviceable)
            .OrderBy(x => x.AssetCode)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var free = await FreeUnitsAsync(bookable, startUtc, endUtc, now, equipmentItemId, cancellationToken);
        return Json(new
        {
            bookableUnits = bookable.Count,
            freeUnits = free.Count,
            maxQuantity = Math.Min(free.Count, MaxUnitsPerRequest)
        });
    }

    [HttpPost("borrower/reservation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReservation(
        ReservationInputModel input, CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(cancellationToken);
        if (profile is null) return Forbid();

        var model = await BuildReservationPageAsync(profile, input, cancellationToken);
        model.OpenDialog = true;
        var selected = model.Selected;
        if (!profile.IsEligible)
            ModelState.AddModelError(string.Empty, "Your borrowing access is currently paused. Contact an administrator.");
        if (input.RequestId == Guid.Empty)
            ModelState.AddModelError(nameof(input.RequestId), "Reload the form and try again.");
        if (selected is not { IsRequestable: true })
            ModelState.AddModelError(nameof(input.EquipmentItemId), "Choose available equipment.");
        else if (input.Quantity < 1 || input.Quantity > selected.MaxQuantity)
            ModelState.AddModelError(nameof(input.Quantity), selected.MaxQuantity == 1
                ? "Only 1 unit of this equipment can be requested."
                : $"Request between 1 and {selected.MaxQuantity} units.");
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

        // A resubmitted form (double click, refresh, back button) shows the reservations it already created.
        var requestIds = RequestIdsFor(input.RequestId, input.Quantity);
        var earlier = await FindRequestsAsync(profile.Id, requestIds, cancellationToken);
        if (earlier.Count > 0)
        {
            if (earlier.All(x => x.Status != ReservationStatus.Cancelled))
                return RedirectToSubmitted(input.EquipmentItemId, earlier.Select(x => x.Id));
            model.Input.RequestId = Guid.NewGuid();
            model.Feedback = "This request form was already used. Review the details and submit again.";
            return View("Reservation", model);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var candidates = new Queue<Guid>(await FreeUnitsAsync(selected!.BookableUnitIds,
            startUtc!.Value, endUtc!.Value, now, input.EquipmentItemId, cancellationToken));
        if (candidates.Count < input.Quantity)
        {
            model.Feedback = candidates.Count == 0
                ? "This equipment is unavailable for the requested time."
                : $"Only {candidates.Count} of the {input.Quantity} units you requested are free for that time. Lower the quantity or choose another time.";
            model.IsConflict = true;
            return View("Reservation", model);
        }

        // Each unit is its own reservation, so every unit goes through the same locking, conflict checks,
        // pending hold and staff approval as before. A batch is not abandoned halfway if the browser disconnects.
        var created = new List<ReservationResult>();
        try
        {
            for (var index = 0; index < input.Quantity; index++)
            {
                while (true)
                {
                    if (!candidates.TryDequeue(out var unitId))
                        throw new ReservationWorkflowException(ReservationErrorCode.ScheduleConflict,
                            "Another borrower just reserved this equipment for the requested time. Lower the quantity or choose another time.");
                    try
                    {
                        created.Add(await reservations.CreateAsync(
                            Actor(),
                            new CreateReservationCommand(requestIds[index], profile.UserId,
                                unitId, startUtc.Value, endUtc.Value,
                                input.Purpose!.Trim(), input.Notes), CancellationToken.None));
                        break;
                    }
                    catch (ReservationWorkflowException exception) when (exception.Code is
                        ReservationErrorCode.ScheduleConflict or ReservationErrorCode.UnavailableEquipment)
                    {
                        // This unit was taken after the availability check; try the next free unit.
                    }
                }
            }
        }
        catch (ReservationWorkflowException exception)
        {
            if (exception.Code == ReservationErrorCode.Forbidden)
            {
                await WithdrawAsync(created);
                return Forbid();
            }
            if (exception.Code == ReservationErrorCode.DuplicateRequest)
            {
                // The same form was submitted twice at once; show what that submission saved.
                var saved = await FindRequestsAsync(profile.Id, requestIds, CancellationToken.None);
                if (saved.Count > 0)
                    return RedirectToSubmitted(input.EquipmentItemId, saved.Select(x => x.Id));
            }
            if (created.Count > 0)
            {
                // All-or-nothing: never leave the borrower with fewer units than they asked for.
                await WithdrawAsync(created);
                model.Input.RequestId = Guid.NewGuid();
            }
            model.Feedback = exception.Message;
            model.IsConflict = exception.Code == ReservationErrorCode.ScheduleConflict;
            return View("Reservation", model);
        }
        return RedirectToSubmitted(input.EquipmentItemId, created.Select(x => x.Id));
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
        string? status, string? from, string? to, string? q, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var profile = await OwnProfileAsync(cancellationToken);
        if (profile is null) return Forbid();

        status = NormalizeStatus(status);
        q = q?.Trim();
        if (q?.Length > 100) q = q[..100];
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
        if (!string.IsNullOrEmpty(q))
            query = query.Where(x => x.EquipmentItem.Name.Contains(q) || x.EquipmentItem.AssetCode.Contains(q) ||
                x.Purpose.Contains(q));

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
            rows, status, from ?? string.Empty, to ?? string.Empty, q ?? string.Empty,
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
            first.AddMonths(-1), first.AddMonths(1), days, selected, slots,
            await EquipmentPicker.LoadAsync(db, clock.GetUtcNow().UtcDateTime, cancellationToken)));
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

    /// <summary>
    /// Builds the catalog. Units that share a category and name (e.g. five "Canon EOS Camera" assets)
    /// are shown as one product whose quantity is the number of units that can be borrowed.
    /// </summary>
    private async Task<ReservationPageViewModel> BuildReservationPageAsync(
        BorrowerProfile profile, ReservationInputModel input, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var units = await db.EquipmentItems.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name).ThenBy(x => x.AssetCode)
            .Select(x => new
            {
                x.Id, x.CategoryId, Category = x.Category.Name, x.Name, x.Description,
                x.ImagePath, x.Location, x.Condition, x.CreatedAtUtc,
                IsBookable = !x.IsMaintenanceHold &&
                    x.Condition != EquipmentCondition.Damaged &&
                    x.Condition != EquipmentCondition.Unserviceable
            })
            .ToListAsync(cancellationToken);
        // Units that are out right now: released on a loan, or inside an approved booking.
        var inUse = (await db.Reservations.AsNoTracking()
            .Where(x => x.Status == ReservationStatus.Released ||
                x.Status == ReservationStatus.Approved && x.StartAtUtc <= now && x.EndAtUtc > now)
            .Select(x => x.EquipmentItemId).Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        var items = units.GroupBy(x => (x.CategoryId, x.Name))
            .Select(group =>
            {
                var bookable = group.Where(x => x.IsBookable).ToList();
                var lead = group.FirstOrDefault(x => x.Id == input.EquipmentItemId)
                    ?? bookable.FirstOrDefault() ?? group.First();
                return new CatalogEquipment(
                    lead.Id, lead.Name, lead.Category,
                    group.Select(x => x.Description).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    group.Select(x => x.ImagePath).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                    group.Select(x => x.Location).Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!).Distinct().ToList(),
                    bookable.Select(x => x.Condition).Distinct().Order().ToList(),
                    group.Select(x => x.Id).ToList(),
                    bookable.Select(x => x.Id).ToList(),
                    bookable.Count(x => !inUse.Contains(x.Id)),
                    group.Max(x => x.CreatedAtUtc));
            })
            .OrderByDescending(x => x.IsRequestable).ThenBy(x => x.Name).ThenBy(x => x.Category)
            .ToList();
        return new ReservationPageViewModel(input, items, profile.IsEligible)
        {
            MinStartLocal = LocalInput(Manila(now).AddMinutes(1))
        };
    }

    /// <summary>
    /// Returns the units free for the whole window, the preferred unit first. This mirrors the
    /// reservation service's conflict rule; the service checks again under its per-item lock.
    /// </summary>
    private async Task<List<Guid>> FreeUnitsAsync(IReadOnlyList<Guid> unitIds,
        DateTime startUtc, DateTime endUtc, DateTime now, Guid preferredUnitId,
        CancellationToken cancellationToken)
    {
        if (unitIds.Count == 0) return [];
        var blocked = await db.Reservations.AsNoTracking()
            .Where(x => unitIds.Contains(x.EquipmentItemId) &&
                x.StartAtUtc < endUtc && x.EndAtUtc > startUtc &&
                (x.Status == ReservationStatus.Approved ||
                 x.Status == ReservationStatus.Released ||
                 x.Status == ReservationStatus.Pending &&
                     x.HoldExpiresAtUtc != null && x.HoldExpiresAtUtc > now && x.StartAtUtc > now))
            .Select(x => x.EquipmentItemId).Distinct()
            .ToListAsync(cancellationToken);
        return unitIds.Where(x => !blocked.Contains(x))
            .OrderByDescending(x => x == preferredUnitId)
            .ToList();
    }

    private async Task<List<(Guid Id, ReservationStatus Status)>> FindRequestsAsync(
        Guid profileId, Guid[] requestIds, CancellationToken cancellationToken) =>
        (await db.Reservations.AsNoTracking()
            .Where(x => x.BorrowerProfileId == profileId && requestIds.Contains(x.RequestId))
            .Select(x => new { x.Id, x.Status })
            .ToListAsync(cancellationToken))
        .Select(x => (x.Id, x.Status)).ToList();

    /// <summary>Cancels reservations created earlier in a batch that could not be completed.</summary>
    private async Task WithdrawAsync(IEnumerable<ReservationResult> created)
    {
        foreach (var reservation in created)
        {
            try
            {
                await reservations.CancelAsync(Actor(), reservation.Id,
                    "Withdrawn automatically: the full requested quantity was not available.",
                    CancellationToken.None);
            }
            catch (ReservationWorkflowException)
            {
                // It stays visible in the borrower's history, where it can be cancelled by hand.
            }
        }
    }

    private RedirectResult RedirectToSubmitted(Guid equipmentItemId, IEnumerable<Guid> reservationIds) =>
        Redirect($"/borrower/reservation?equipmentItemId={equipmentItemId:D}&state=submitted" +
            string.Concat(reservationIds.Select(x => $"&id={x:D}")));

    /// <summary>
    /// The first unit keeps the form's request ID, so a one-unit request is stored exactly as before.
    /// Later units get IDs derived from it, so a resubmitted form can find every unit it created.
    /// </summary>
    private static Guid[] RequestIdsFor(Guid requestId, int quantity)
    {
        var ids = new Guid[Math.Max(1, quantity)];
        ids[0] = requestId;
        Span<byte> seed = stackalloc byte[20];
        for (var index = 1; index < ids.Length; index++)
        {
            requestId.TryWriteBytes(seed);
            BinaryPrimitives.WriteInt32LittleEndian(seed[16..], index);
            ids[index] = new Guid(SHA256.HashData(seed)[..16]);
        }
        return ids;
    }

    private DateTime? ParseManilaInput(string? value, string field)
    {
        if (!TryParseManila(value, out var utc))
        {
            ModelState.AddModelError(field, "Choose a valid date and time in Manila time.");
            return null;
        }
        return utc;
    }

    private static bool TryParseManila(string? value, out DateTime utc)
    {
        utc = default;
        if (!DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local) || local.Year is < 2000 or > 2100)
            return false;
        utc = ToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
        return true;
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
    public int Quantity { get; set; } = 1;
    public string? StartLocal { get; set; }
    public string? EndLocal { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReservationPageViewModel(
    ReservationInputModel input, IReadOnlyList<CatalogEquipment> items, bool isEligible)
{
    public ReservationInputModel Input { get; } = input;
    public IReadOnlyList<CatalogEquipment> Items { get; } = items;
    public bool IsEligible { get; } = isEligible;
    public IReadOnlyList<string> Categories => Items.Select(x => x.Category).Distinct().Order().ToList();
    public CatalogEquipment? Selected => Items.FirstOrDefault(x => x.UnitIds.Contains(Input.EquipmentItemId));
    public IReadOnlyList<SubmittedReservation> Submitted { get; set; } = [];
    public string? Feedback { get; set; }
    public bool IsConflict { get; set; }
    public bool OpenDialog { get; set; }
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
    public int MaxQuantity => Math.Min(BookableUnits, BorrowerController.MaxUnitsPerRequest);
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
public static class EquipmentIcon
{
    public static string For(string name, string category)
    {
        var text = $"{name} {category}".ToLowerInvariant();
        if (new[] { "laptop", "computer", "notebook", "macbook", "chromebook" }.Any(text.Contains)) return "laptop";
        if (new[] { "camera", "camcorder", "dslr", "lens", "tripod" }.Any(text.Contains)) return "camera";
        if (text.Contains("projector")) return "projector";
        if (new[] { "mic", "audio", "speaker", "sound", "headphone" }.Any(text.Contains)) return "audio";
        return "box";
    }
}
