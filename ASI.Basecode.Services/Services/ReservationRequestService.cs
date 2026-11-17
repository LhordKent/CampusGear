using System.Buffers.Binary;
using System.Security.Cryptography;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    public sealed class ReservationRequestService : ServiceBase, IReservationRequestService
    {
        private readonly IBorrowerReadRepository _reads;
        private readonly IReservationService _reservations;
        private readonly TimeProvider _timeProvider;
        private readonly IEquipmentCatalogService _catalog;
        private readonly IEquipmentAvailabilityService _availability;

        public ReservationRequestService(
            IEquipmentAvailabilityService availability,
            IBorrowerReadRepository reads,
            IReservationService reservations,
            TimeProvider clock,
            IEquipmentCatalogService catalog
        )
        {
            _reads = reads;
            _availability = availability;
            _reservations = reservations;
            _timeProvider = clock;
            _catalog = catalog;
        }

        public async Task<OperationResult<ReservationPageViewModel>> CreateReservationAsync(
            RequestContext actor,
            ReservationRequestModel input,
            CancellationToken cancellationToken
        )
        {
            var profile = await OwnProfileAsync(actor, cancellationToken);
            if (profile is null)
            {
                return new(OperationOutcome.Forbidden);
            }

            var errors = new List<FieldError>();
            var model = await _catalog.GetRequestPageAsync(profile.IsEligible, input, cancellationToken);
            model.OpenDialog = true;
            var selected = model.Selected;
            if (!profile.IsEligible)
            {
                errors.Add(new(string.Empty, Errors.BorrowingPaused));
            }

            if (input.RequestId == Guid.Empty)
            {
                errors.Add(new(nameof(input.RequestId), Errors.InvalidRequestId));
            }

            if (selected is not { IsRequestable: true })
            {
                errors.Add(new(nameof(input.EquipmentItemId), Errors.ReservationRequestEquipmentRequired));
            }
            else if (input.Quantity < 1 || input.Quantity > selected.MaxQuantity)
            {
                errors.Add(
                    new(
                        nameof(input.Quantity),
                        selected.MaxQuantity == 1
                            ? Errors.SingleUnitOnly
                            : $"Request between 1 and {selected.MaxQuantity} units."
                    )
                );
            }

            if (string.IsNullOrWhiteSpace(input.Purpose))
            {
                errors.Add(new(nameof(input.Purpose), Errors.ReservationRequestPurposeRequired));
            }
            else if (input.Purpose.Trim().Length > 500)
            {
                errors.Add(new(nameof(input.Purpose), Errors.PurposeTooLong));
            }

            if (input.Notes?.Trim().Length > 2000)
            {
                errors.Add(new(nameof(input.Notes), Errors.NotesTooLong));
            }

            var startUtc = ParseManilaInput(input.StartLocal, nameof(input.StartLocal), errors);
            var endUtc = ParseManilaInput(input.EndLocal, nameof(input.EndLocal), errors);
            if (startUtc is not null && endUtc is not null && startUtc >= endUtc)
            {
                errors.Add(new(nameof(input.EndLocal), Errors.ReservationRequestInvalidDateRange));
            }

            if (startUtc is not null && startUtc <= _timeProvider.GetUtcNow().UtcDateTime)
            {
                errors.Add(new(nameof(input.StartLocal), Errors.ReservationRequestFutureStartRequired));
            }

            // Fair-use limits, shown on the field they concern. ReservationService enforces them again on create.
            if (startUtc is not null && startUtc > _timeProvider.GetUtcNow().UtcDateTime + BorrowingLimits.BookingWindow)
            {
                errors.Add(new(nameof(input.StartLocal), string.Format(Errors.BookingTooFarAhead, BorrowingLimits.BookingWindowDays)));
            }

            if (startUtc is not null && endUtc is not null && endUtc - startUtc > BorrowingLimits.MaxBookingDuration)
            {
                errors.Add(new(nameof(input.EndLocal), string.Format(Errors.BookingTooLong, BorrowingLimits.MaxBookingDays)));
            }

            if (errors.Count > 0)
            {
                return new(OperationOutcome.Invalid, model, Errors: errors);
            }

            // A resubmitted form (double click, refresh, back button) shows the reservations it already created.
            var requestIds = RequestIdsFor(input.RequestId, input.Quantity);
            var earlier = await FindRequestsAsync(profile.Id, requestIds, cancellationToken);
            if (earlier.Count > 0)
            {
                if (earlier.All(x => x.Status != ReservationStatus.Cancelled))
                {
                    return Submitted(model, earlier.Select(x => x.Id));
                }

                model.Input.RequestId = Guid.NewGuid();
                model.Feedback = Errors.ReusedRequestForm;
                return new(OperationOutcome.Invalid, model, Errors: errors);
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var candidates = new Queue<Guid>(
                await _availability.FindFreeUnitsAsync(
                    selected!.BookableUnitIds,
                    startUtc!.Value,
                    endUtc!.Value,
                    now,
                    input.EquipmentItemId,
                    cancellationToken
                )
            );
            if (candidates.Count < input.Quantity)
            {
                model.Feedback =
                    candidates.Count == 0
                        ? Errors.ScheduleUnavailable
                        : $"Only {candidates.Count} of the {input.Quantity} units you requested are free for that time. "
                            + "Lower the quantity or choose another time.";
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
                        {
                            throw new ReservationWorkflowException(
                                ReservationErrorCode.ScheduleConflict,
                                Errors.ConcurrentBooking
                            );
                        }

                        try
                        {
                            created.Add(
                                await _reservations.CreateAsync(
                                    new ReservationActor(actor.UserId!, actor.IpAddress, actor.CorrelationId),
                                    new CreateReservationCommand(
                                        requestIds[index],
                                        profile.UserId,
                                        unitId,
                                        startUtc.Value,
                                        endUtc.Value,
                                        input.Purpose!.Trim(),
                                        input.Notes
                                    ),
                                    CancellationToken.None
                                )
                            );
                            break;
                        }
                        catch (ReservationWorkflowException exception)
                            when (exception.Code is ReservationErrorCode.ScheduleConflict or ReservationErrorCode.UnavailableEquipment)
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
                    {
                        return Submitted(model, saved.Select(x => x.Id));
                    }
                }

                if (created.Count > 0)
                {
                    // Compensate earlier per-unit commits; cancellation can itself fail during an outage.
                    await WithdrawAsync(actor, created);
                    model.Input.RequestId = Guid.NewGuid();
                }

                model.Feedback = exception.Message;
                model.IsConflict = exception.Code == ReservationErrorCode.ScheduleConflict;
                return new(OperationOutcome.Invalid, model, Errors: errors);
            }

            return Submitted(model, created.Select(x => x.Id));
        }

        private Task<BorrowerProfile?> OwnProfileAsync(RequestContext actor, CancellationToken ct)
        {
            return _reads.FindProfileAsync(actor.UserId, ct);
        }

        private async Task<List<RequestRecord>> FindRequestsAsync(Guid profileId, Guid[] requestIds, CancellationToken ct)
        {
            return (await _reads.FindRequestsAsync(profileId, requestIds, ct)).ToList();
        }

        /// <summary>Cancels reservations created earlier in a batch that could not be completed.</summary>
        private async Task WithdrawAsync(RequestContext actor, IEnumerable<ReservationResult> created)
        {
            foreach (var reservation in created)
            {
                try
                {
                    await _reservations.CancelAsync(
                        new ReservationActor(actor.UserId!, actor.IpAddress, actor.CorrelationId),
                        reservation.Id,
                        Common.BatchWithdrawn,
                        CancellationToken.None
                    );
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
            if (!ManilaTime.TryParseInput(value, out var utc))
            {
                errors.Add(new(field, Errors.InvalidManilaDate));
                return null;
            }

            return utc;
        }
    }
}
