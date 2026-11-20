using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;

// =====================================================================
// EQUIPMENT CATALOG SERVICE  -  what students see when browsing and booking (read-only).
// Units with the same name and category are shown as ONE product with a quantity,
// e.g. five "Canon EOS Camera" units = one card that says "5 available".
// =====================================================================
namespace ASI.Basecode.Services.Services
{
    public sealed class EquipmentCatalogService : ServiceBase, IEquipmentCatalogService
    {
        private readonly IBorrowerReadRepository _reads;
        private readonly UserManager<User> _userManager;
        private readonly TimeProvider _timeProvider;
        private const int MaxUnitsPerRequest = BorrowingLimits.MaxUnitsPerRequest;
        private readonly IEquipmentAvailabilityService _availability;

        public EquipmentCatalogService(
            IEquipmentAvailabilityService availability,
            IBorrowerReadRepository reads,
            UserManager<User> users,
            TimeProvider clock
        )
        {
            _reads = reads;
            _availability = availability;
            _userManager = users;
            _timeProvider = clock;
        }

        // The student home page: catalog, their active bookings, and notices (e.g. borrowing paused).
        public async Task<OperationResult<BorrowerHomeViewModel>> HomeAsync(RequestContext actor, CancellationToken cancellationToken)
        {
            var profile = await OwnProfileAsync(actor, cancellationToken);
            if (profile is null)
            {
                return new(OperationOutcome.Forbidden);
            }

            var user = await _userManager.FindByIdAsync(actor.UserId!);
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var catalog = await GetRequestPageAsync(profile.IsEligible, new ReservationRequestModel(), cancellationToken);
            var home = await _reads.HomeAsync(profile.Id, now, cancellationToken);
            var upcoming = home.Upcoming;
            return OperationResult<BorrowerHomeViewModel>.Ok(
                new BorrowerHomeViewModel(
                    user?.FirstName is { Length: > 0 } first ? first : "there",
                    profile.IsEligible,
                    catalog
                        .Items.Where(x => x.FreeNowUnits > 0)
                        .OrderByDescending(x => x.FreeNowUnits)
                        .ThenBy(x => x.Name)
                        .Take(6)
                        .ToList(),
                    catalog
                        .Items.GroupBy(x => x.Category)
                        .Select(x => new HomeCategory(x.Key, x.Count(), x.Sum(item => item.FreeNowUnits)))
                        .OrderBy(x => x.Name)
                        .ToList(),
                    catalog.Items.Count,
                    catalog.Items.Sum(x => x.FreeNowUnits),
                    upcoming
                        .Select(x => new HomeReservation(
                            x.Id,
                            x.EquipmentItem.Name,
                            x.EquipmentItem.AssetCode,
                            ManilaTime.Format(x.StartAtUtc),
                            ManilaTime.Format(x.EndAtUtc),
                            ReservationDisplay.Status(x, now)
                        ))
                        .ToList(),
                    home.Pending,
                    home.Approved,
                    home.Borrowed,
                    home.Overdue,
                    home.Completed
                )
            );
        }

        // The booking page for one product.
        public async Task<OperationResult<ReservationPageViewModel>> ReservationAsync(
            RequestContext actor,
            Guid? equipmentItemId,
            string? state,
            Guid[]? id,
            CancellationToken cancellationToken
        )
        {
            var profile = await OwnProfileAsync(actor, cancellationToken);
            if (profile is null)
            {
                return new(OperationOutcome.Forbidden);
            }

            var localNow = ManilaTime.Local(_timeProvider.GetUtcNow().UtcDateTime);
            var start = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0).AddHours(1);
            var input = new ReservationRequestModel
            {
                RequestId = Guid.NewGuid(),
                EquipmentItemId = equipmentItemId ?? Guid.Empty,
                StartLocal = ManilaTime.Input(start),
                EndLocal = ManilaTime.Input(start.AddHours(2)),
            };
            var model = await GetRequestPageAsync(profile.IsEligible, input, cancellationToken);
            // A query string is never proof of submission. Confirm the persisted records belong to this borrower.
            if (state == "submitted" && id is { Length: > 0 })
            {
                var ids = id.Distinct().Take(MaxUnitsPerRequest).ToList();
                var submitted = await _reads.SubmittedAsync(profile.Id, ids, cancellationToken);
                model.Submitted = submitted
                    .Select(x => new SubmittedReservation(
                        x.Id,
                        x.EquipmentItem.Name,
                        x.EquipmentItem.AssetCode,
                        ManilaTime.Format(x.StartAtUtc),
                        ManilaTime.Format(x.EndAtUtc),
                        x.HoldExpiresAtUtc is { } hold ? ManilaTime.Format(hold) : null,
                        x.Status
                    ))
                    .ToList();
            }

            // A deep link such as the calendar's "Reserve this equipment" opens that item's request dialog.
            model.OpenDialog = model.Submitted.Count == 0 && model.Selected is not null;
            return OperationResult<ReservationPageViewModel>.Ok(model);
        }

        // WBS 25.00 | Count available units in the selected equipment group for the preview.
        public async Task<OperationResult<AvailabilityResult>> AvailabilityAsync(
            RequestContext actor,
            Guid equipmentItemId,
            string? start,
            string? end,
            CancellationToken cancellationToken
        )
        {
            var profile = await OwnProfileAsync(actor, cancellationToken);
            if (profile is null)
            {
                return new(OperationOutcome.Forbidden);
            }

            var unit = (await _reads.CatalogAsync(cancellationToken)).SingleOrDefault(x => x.Id == equipmentItemId);
            if (unit is null)
            {
                return new(OperationOutcome.NotFound);
            }

            if (!ManilaTime.TryParseInput(start, out var startUtc) || !ManilaTime.TryParseInput(end, out var endUtc))
            {
                return OperationResult<AvailabilityResult>.Invalid(Errors.InvalidBookingDates);
            }

            if (startUtc >= endUtc)
            {
                return OperationResult<AvailabilityResult>.Invalid(Errors.EquipmentCatalogInvalidDateRange);
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (startUtc <= now)
            {
                return OperationResult<AvailabilityResult>.Invalid(Errors.EquipmentCatalogFutureStartRequired);
            }

            var bookable = (await _reads.CatalogAsync(cancellationToken))
                .Where(x => x.CategoryId == unit.CategoryId && x.Name == unit.Name && x.IsBookable)
                .OrderBy(x => x.AssetCode)
                .Select(x => x.Id)
                .ToList();
            var free = await _availability.FindFreeUnitsAsync(bookable, startUtc, endUtc, now, equipmentItemId, cancellationToken);
            return OperationResult<AvailabilityResult>.Ok(new(bookable.Count, free.Count, Math.Min(free.Count, MaxUnitsPerRequest)));
        }

        /// <summary>Busy times for every bookable unit of a product over a window of about three months.</summary>
        // Busy times for the date picker (about three months ahead).
        public async Task<OperationResult<ReservationSchedule>> ScheduleAsync(
            RequestContext actor,
            Guid equipmentItemId,
            string? from,
            CancellationToken cancellationToken
        )
        {
            const int windowDays = 92;
            var profile = await OwnProfileAsync(actor, cancellationToken);
            if (profile is null)
            {
                return new(OperationOutcome.Forbidden);
            }

            var units = await _reads.CatalogAsync(cancellationToken);
            var unit = units.SingleOrDefault(x => x.Id == equipmentItemId);
            if (unit is null)
            {
                return new(OperationOutcome.NotFound);
            }

            // WBS 25.00 | Use the same equipment grouping for the picker and availability preview.
            var bookable = units
                .Where(x => x.CategoryId == unit.CategoryId && x.Name == unit.Name && x.IsBookable)
                .OrderBy(x => x.AssetCode)
                .Select(x => x.Id)
                .ToList();
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var localNow = ManilaTime.Local(now);
            var today = DateOnly.FromDateTime(localNow);
            var firstDay = ManilaTime.TryParseDate(from, out var requested) && requested > today ? requested : today;
            var fromLocal = firstDay.ToDateTime(TimeOnly.MinValue);
            var toLocal = fromLocal.AddDays(windowDays);
            var busy = await _availability.BusyIntervalsAsync(
                bookable,
                ManilaTime.Utc(fromLocal),
                ManilaTime.Utc(toLocal),
                now,
                cancellationToken
            );
            var index = bookable.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            return OperationResult<ReservationSchedule>.Ok(
                new ReservationSchedule(
                    bookable.Count,
                    Math.Min(bookable.Count, MaxUnitsPerRequest),
                    ManilaTime.Input(localNow),
                    ManilaTime.Input(fromLocal),
                    ManilaTime.Input(toLocal),
                    busy.Select(x => new ScheduleBusy(
                            index[x.EquipmentItemId],
                            ManilaTime.Input(ManilaTime.Local(x.StartAtUtc)),
                            ManilaTime.Input(ManilaTime.Local(x.EndAtUtc))
                        ))
                        .ToList()
                )
            );
        }

        private Task<BorrowerProfile?> OwnProfileAsync(RequestContext actor, CancellationToken ct)
        {
            return _reads.FindProfileAsync(actor.UserId, ct);
        }

        /// <summary>

        /// Builds the catalog. Units that share a category and name (e.g. five "Canon EOS Camera" assets)

        /// are shown as one product whose quantity is the number of units that can be borrowed.

        /// </summary>
        // Builds the product list: groups units into products and counts how many can be borrowed.
        public async Task<ReservationPageViewModel> GetRequestPageAsync(
            bool isEligible,
            ReservationRequestModel input,
            CancellationToken cancellationToken
        )
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var units = await _reads.CatalogAsync(cancellationToken);
            // Units that are out right now: released on a loan, or inside an approved booking.
            var inUse = (await _reads.InUseAsync(now, cancellationToken)).ToHashSet();
            var items = units
                .GroupBy(x => (x.CategoryId, x.Name))
                .Select(group =>
                {
                    var bookable = group.Where(x => x.IsBookable).ToList();
                    var lead = group.FirstOrDefault(x => x.Id == input.EquipmentItemId) ?? bookable.FirstOrDefault() ?? group.First();
                    return new CatalogEquipment(
                        lead.Id,
                        lead.Name,
                        lead.Category,
                        group.Select(x => x.Description).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        group.Select(x => x.ImagePath).FirstOrDefault(x => !string.IsNullOrEmpty(x)),
                        group.Select(x => x.Location).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct().ToList(),
                        bookable.Select(x => x.Condition).Distinct().Order().ToList(),
                        group.Select(x => x.Id).ToList(),
                        bookable.Select(x => x.Id).ToList(),
                        bookable.Count(x => !inUse.Contains(x.Id)),
                        group.Max(x => x.CreatedAtUtc)
                    );
                })
                .OrderByDescending(x => x.IsRequestable)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.Category)
                .ToList();
            return new ReservationPageViewModel(input, items, isEligible)
            {
                MinStartLocal = ManilaTime.Input(ManilaTime.Local(now).AddMinutes(1)),
            };
        }
    }
}
