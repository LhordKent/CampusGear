using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using CampusGear.Controllers;
using CampusGear.Data.Interfaces;
using CampusGear.Data.QueryModels;
using CampusGear.Models;
using CampusGear.Services.Reservations;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;
namespace CampusGear.Services;
public sealed class BorrowerService(IBorrowerReadRepository reads, IEquipmentAvailabilityService availability,
    UserManager<ApplicationUser> users, IReservationService reservations, TimeProvider clock) : IBorrowerService
{
    private static readonly TimeZoneInfo ManilaZone = ResolveManilaZone();
    private const int HistoryPageSize = 12;
    private const int MaxUnitsPerRequest = BorrowingLimits.MaxUnitsPerRequest;
    public async Task<OperationResult<BorrowerHomeViewModel>> HomeAsync(RequestContext actor, CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(actor, cancellationToken);
        if (profile is null) return new(OperationOutcome.Forbidden);
        var user = await users.FindByIdAsync(actor.UserId!);

        var now = clock.GetUtcNow().UtcDateTime;
        var catalog = await BuildReservationPageAsync(profile, new ReservationCommand(), cancellationToken);
        var home = await reads.HomeAsync(profile.Id, now, cancellationToken);
        var upcoming = home.Upcoming;

        return OperationResult<BorrowerHomeViewModel>.Ok(new BorrowerHomeViewModel(
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
            home.Pending,
            home.Approved,
            home.Borrowed,
            home.Overdue,
            home.Completed));
    }

    public async Task<OperationResult<ReservationPageViewModel>> ReservationAsync(RequestContext actor,
        Guid? equipmentItemId, string? state, Guid[]? id, CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(actor, cancellationToken);
        if (profile is null) return new(OperationOutcome.Forbidden);

        var localNow = Manila(clock.GetUtcNow().UtcDateTime);
        var start = new DateTime(localNow.Year, localNow.Month, localNow.Day,
            localNow.Hour, 0, 0).AddHours(1);
        var input = new ReservationCommand
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
            var submitted = await reads.SubmittedAsync(profile.Id, ids, cancellationToken);
            model.Submitted = submitted.Select(x => new SubmittedReservation(
                x.Id, x.EquipmentItem.Name, x.EquipmentItem.AssetCode,
                FormatManila(x.StartAtUtc), FormatManila(x.EndAtUtc),
                x.HoldExpiresAtUtc is { } hold ? FormatManila(hold) : null,
                x.Status)).ToList();
        }
        // A deep link such as the calendar's "Reserve this equipment" opens that item's request dialog.
        model.OpenDialog = model.Submitted.Count == 0 && model.Selected is not null;

        return OperationResult<ReservationPageViewModel>.Ok(model);
    }

    public async Task<OperationResult<AvailabilityResult>> AvailabilityAsync(RequestContext actor,
        Guid equipmentItemId, string? start, string? end, CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(actor, cancellationToken);
        if (profile is null) return new(OperationOutcome.Forbidden);

        var unit = (await reads.CatalogAsync(cancellationToken)).SingleOrDefault(x => x.Id == equipmentItemId);
        if (unit is null) return new(OperationOutcome.NotFound);
        if (!TryParseManila(start, out var startUtc) || !TryParseManila(end, out var endUtc))
            return OperationResult<AvailabilityResult>.Invalid("Choose a valid borrowing and return date and time.");
        if (startUtc >= endUtc)
            return OperationResult<AvailabilityResult>.Invalid("The return time must be after the borrowing time.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (startUtc <= now)
            return OperationResult<AvailabilityResult>.Invalid("Choose a future borrowing time.");

        var bookable = (await reads.CatalogAsync(cancellationToken)).Where(x => x.CategoryId == unit.CategoryId && x.Name == unit.Name && x.IsBookable).OrderBy(x => x.AssetCode).Select(x => x.Id).ToList();
        var free = await FreeUnitsAsync(bookable, startUtc, endUtc, now, equipmentItemId, cancellationToken);
        return OperationResult<AvailabilityResult>.Ok(new(bookable.Count, free.Count, Math.Min(free.Count, MaxUnitsPerRequest)));
    }

    public async Task<OperationResult<ReservationPageViewModel>> CreateReservationAsync(RequestContext actor,
        ReservationCommand input, CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(actor, cancellationToken);
        if (profile is null) return new(OperationOutcome.Forbidden);

        var errors = new List<FieldError>();
        var model = await BuildReservationPageAsync(profile, input, cancellationToken);
        model.OpenDialog = true;
        var selected = model.Selected;
        if (!profile.IsEligible)
            errors.Add(new(string.Empty, "Your borrowing access is currently paused. Contact an administrator."));
        if (input.RequestId == Guid.Empty)
            errors.Add(new(nameof(input.RequestId), "Reload the form and try again."));
        if (selected is not { IsRequestable: true })
            errors.Add(new(nameof(input.EquipmentItemId), "Choose available equipment."));
        else if (input.Quantity < 1 || input.Quantity > selected.MaxQuantity)
            errors.Add(new(nameof(input.Quantity), selected.MaxQuantity == 1
                ? "Only 1 unit of this equipment can be requested."
                : $"Request between 1 and {selected.MaxQuantity} units."));
        if (string.IsNullOrWhiteSpace(input.Purpose))
            errors.Add(new(nameof(input.Purpose), "Enter the purpose of your request."));
        else if (input.Purpose.Trim().Length > 500)
            errors.Add(new(nameof(input.Purpose), "Use 500 characters or fewer."));
        if (input.Notes?.Trim().Length > 2000)
            errors.Add(new(nameof(input.Notes), "Use 2000 characters or fewer."));

        var startUtc = ParseManilaInput(input.StartLocal, nameof(input.StartLocal), errors);
        var endUtc = ParseManilaInput(input.EndLocal, nameof(input.EndLocal), errors);
        if (startUtc is not null && endUtc is not null && startUtc >= endUtc)
            errors.Add(new(nameof(input.EndLocal), "The end time must be after the start time."));
        if (startUtc is not null && startUtc <= clock.GetUtcNow().UtcDateTime)
            errors.Add(new(nameof(input.StartLocal), "Choose a future start time."));

        if (errors.Count > 0)
            return new(OperationOutcome.Invalid, model, Errors: errors);

        // A resubmitted form (double click, refresh, back button) shows the reservations it already created.
        var requestIds = RequestIdsFor(input.RequestId, input.Quantity);
        var earlier = await FindRequestsAsync(profile.Id, requestIds, cancellationToken);
        if (earlier.Count > 0)
        {
            if (earlier.All(x => x.Status != ReservationStatus.Cancelled))
                return Submitted(model, earlier.Select(x => x.Id));
            model.Input.RequestId = Guid.NewGuid();
            model.Feedback = "This request form was already used. Review the details and submit again.";
            return new(OperationOutcome.Invalid, model, Errors: errors);
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
            return new(OperationOutcome.Invalid, model, Errors: errors);
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
                            new ReservationActor(actor.UserId!, actor.IpAddress, actor.CorrelationId),
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
                await WithdrawAsync(actor, created);
                return new(OperationOutcome.Forbidden);
            }
            if (exception.Code == ReservationErrorCode.DuplicateRequest)
            {
                // The same form was submitted twice at once; show what that submission saved.
                var saved = await FindRequestsAsync(profile.Id, requestIds, CancellationToken.None);
                if (saved.Count > 0)
                    return Submitted(model, saved.Select(x => x.Id));
            }
            if (created.Count > 0)
            {
                // All-or-nothing: never leave the borrower with fewer units than they asked for.
                await WithdrawAsync(actor, created);
                model.Input.RequestId = Guid.NewGuid();
            }
            model.Feedback = exception.Message;
            model.IsConflict = exception.Code == ReservationErrorCode.ScheduleConflict;
            return new(OperationOutcome.Invalid, model, Errors: errors);
        }
        return Submitted(model, created.Select(x => x.Id));
    }


    public async Task<OperationResult<HistoryPageViewModel>> HistoryAsync(RequestContext actor,
        string? status, string? from, string? to, string? q, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var profile = await OwnProfileAsync(actor, cancellationToken);
        if (profile is null) return new(OperationOutcome.Forbidden);

        status = NormalizeStatus(status);
        q = q?.Trim();
        if (q?.Length > 100) q = q[..100];
        var now = clock.GetUtcNow().UtcDateTime;
        var errors = new List<FieldError>();
        DateTime? fromUtcValue = null, toUtcValue = null;
        if (TryParseDate(from, out var fromDate))
        {
            var fromUtc = ToUtc(fromDate.ToDateTime(TimeOnly.MinValue));
            fromUtcValue = fromUtc;
        }
        else if (!string.IsNullOrEmpty(from))
            errors.Add(new("from", "Choose a valid from date."));
        if (TryParseDate(to, out var toDate) && toDate < DateOnly.MaxValue)
        {
            var toUtc = ToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
            toUtcValue = toUtc;
        }
        else if (!string.IsNullOrEmpty(to))
            errors.Add(new("to", "Choose a valid to date."));
        if (TryParseDate(from, out fromDate) && TryParseDate(to, out toDate) && fromDate > toDate)
            errors.Add(new("to", "The to date must be on or after the from date."));

        var result = await reads.HistoryAsync(profile.Id, status, q, fromUtcValue, toUtcValue, now, page, cancellationToken);
        var records = result.Rows;
        var count = result.Total; var pageCount = result.Pages; page = result.Page;

        var rows = records.Select(x => new HistoryRow(
            x.Id, x.EquipmentItem.Name, x.EquipmentItem.AssetCode,
            FormatManila(x.StartAtUtc), FormatManila(x.EndAtUtc),
            HistoryStatus(x, now), x.Purpose,
            (x.Status is ReservationStatus.Pending or ReservationStatus.Approved) &&
                (x.Status != ReservationStatus.Pending || x.HoldExpiresAtUtc > now),
            x.Loan?.ReturnedAtUtc is { } returned ? FormatManila(returned) : null,
            x.Loan?.ConditionAtReturn?.ToString())).ToList();

        return OperationResult<HistoryPageViewModel>.Ok(new HistoryPageViewModel(
            rows, status, from ?? string.Empty, to ?? string.Empty, q ?? string.Empty,
            page, pageCount, count, null)) with
        { Errors = errors };
    }

    public async Task<OperationResult<CalendarPageViewModel>> CalendarAsync(RequestContext actor,
        int? year, int? month, Guid? equipmentItemId, string? day,
        CancellationToken cancellationToken)
    {
        var profile = await OwnProfileAsync(actor, cancellationToken);
        if (profile is null) return new(OperationOutcome.Forbidden);

        var localToday = DateOnly.FromDateTime(Manila(clock.GetUtcNow().UtcDateTime));
        var chosenYear = year is >= 2000 and <= 2100 ? year.Value : localToday.Year;
        var chosenMonth = month is >= 1 and <= 12 ? month.Value : localToday.Month;
        var first = new DateOnly(chosenYear, chosenMonth, 1);
        var gridStart = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        var gridEnd = gridStart.AddDays(42);
        var gridStartUtc = ToUtc(gridStart.ToDateTime(TimeOnly.MinValue));
        var gridEndUtc = ToUtc(gridEnd.ToDateTime(TimeOnly.MinValue));

        var items = (await reads.CalendarItemsAsync(cancellationToken)).Select(x => new CalendarEquipment(x.Id, x.Name, x.AssetCode, x.MaintenanceHold)).ToList();
        var item = items.FirstOrDefault(x => x.Id == equipmentItemId) ?? items.FirstOrDefault();

        var bookings = item is null
            ? new List<Reservation>()
            : (await reads.CalendarBookingsAsync(item.Id, gridStartUtc, gridEndUtc, clock.GetUtcNow().UtcDateTime, false, cancellationToken)).ToList();

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
            var overlapping = bookings.Where(x => x.StartAtUtc < dayEnd && CustodianTime.BookedUntil(x) > dayStart &&
                (x.Status != ReservationStatus.Pending || x.HoldExpiresAtUtc > now)).ToList();
            var state = item?.IsMaintenanceHold == true ? "Maintenance"
                : overlapping.Any(x => x.Status is ReservationStatus.Released or ReservationStatus.Completed) ? "Borrowed"
                : overlapping.Count > 0 ? "Reserved" : "Available";
            days.Add(new CalendarDay(date, date.Month == chosenMonth, date == localToday,
                date == selected, state, overlapping.Count));
        }

        var selectedStart = ToUtc(selected.ToDateTime(TimeOnly.MinValue));
        var selectedEnd = ToUtc(selected.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var slots = bookings.Where(x => x.StartAtUtc < selectedEnd && CustodianTime.BookedUntil(x) > selectedStart &&
                (x.Status != ReservationStatus.Pending || x.HoldExpiresAtUtc > now))
            .OrderBy(x => x.StartAtUtc)
            .Select(x => new CalendarSlot(
                Manila(x.StartAtUtc).ToString("h:mm tt", CultureInfo.InvariantCulture),
                Manila(CustodianTime.BookedUntil(x)).ToString("h:mm tt", CultureInfo.InvariantCulture),
                x.Status is ReservationStatus.Released or ReservationStatus.Completed ? "Borrowed" : "Reserved"))
            .ToList();

        return OperationResult<CalendarPageViewModel>.Ok(new CalendarPageViewModel(
            items, item, chosenYear, chosenMonth, first.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            first.AddMonths(-1), first.AddMonths(1), days, selected, slots,
            await availability.GetPickerAsync(clock.GetUtcNow().UtcDateTime, cancellationToken)));
    }

    private Task<BorrowerProfile?> OwnProfileAsync(RequestContext actor, CancellationToken ct) => reads.FindProfileAsync(actor.UserId, ct);

    /// <summary>
    /// Builds the catalog. Units that share a category and name (e.g. five "Canon EOS Camera" assets)
    /// are shown as one product whose quantity is the number of units that can be borrowed.
    /// </summary>
    private async Task<ReservationPageViewModel> BuildReservationPageAsync(
        BorrowerProfile profile, ReservationCommand input, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var units = await reads.CatalogAsync(cancellationToken);
        // Units that are out right now: released on a loan, or inside an approved booking.
        var inUse = (await reads.InUseAsync(now, cancellationToken)).ToHashSet();

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
        var blocked = await reads.BlockedAsync(unitIds, startUtc, endUtc, now, cancellationToken);
        return unitIds.Where(x => !blocked.Contains(x))
            .OrderByDescending(x => x == preferredUnitId)
            .ToList();
    }

    private async Task<List<RequestRecord>> FindRequestsAsync(Guid profileId, Guid[] requestIds, CancellationToken ct) =>
        (await reads.FindRequestsAsync(profileId, requestIds, ct)).ToList();

    /// <summary>Cancels reservations created earlier in a batch that could not be completed.</summary>
    private async Task WithdrawAsync(RequestContext actor, IEnumerable<ReservationResult> created)
    {
        foreach (var reservation in created)
        {
            try
            {
                await reservations.CancelAsync(new ReservationActor(actor.UserId!, actor.IpAddress, actor.CorrelationId), reservation.Id,
                    "Withdrawn automatically: the full requested quantity was not available.",
                    CancellationToken.None);
            }
            catch (ReservationWorkflowException)
            {
                // It stays visible in the borrower's history, where it can be cancelled by hand.
            }
        }
    }

    private static OperationResult<ReservationPageViewModel> Submitted(ReservationPageViewModel model, IEnumerable<Guid> ids)
    {
        model.CompletedIds = ids.ToList();
        return OperationResult<ReservationPageViewModel>.Ok(model);
    }

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

    private DateTime? ParseManilaInput(string? value, string field, List<FieldError> errors)
    {
        if (!TryParseManila(value, out var utc))
        {
            errors.Add(new(field, "Choose a valid date and time in Manila time."));
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
        if (CustodianTime.IsReturnedEarly(reservation.Loan))
            return "Returned early";
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
