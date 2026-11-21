using System.Security.Claims;
using AutoMapper;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// =====================================================================
// BORROWER CONTROLLER  (students only)
// Everything a student does: browse equipment, request a booking, cancel it, see history and calendar.
//
// FLOW (making a booking):
// Reservation page -> CreateReservation() -> ReservationRequestService (fair-use rules)
// -> ReservationService.CreateAsync (locks the item, checks conflicts) -> database.
// The new booking is "Pending" until a custodian approves it.
// =====================================================================
namespace ASI.Basecode.WebApp.Controllers
{
    // WBS 45.00 | Protect the borrower actions even when a request comes directly to a route.
    [Authorize(Roles = RoleNames.Borrower)]
    public sealed class BorrowerController : Mvc.ControllerBase<BorrowerController>
    {
        private readonly IEquipmentCatalogService _catalog;
        private readonly IReservationRequestService _requests;
        private readonly IBorrowingHistoryService _history;
        private readonly IAvailabilityCalendarService _calendars;
        private readonly IReservationService _reservations;

        public BorrowerController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IEquipmentCatalogService catalog,
            IReservationRequestService requests,
            IBorrowingHistoryService history,
            IAvailabilityCalendarService calendars,
            IReservationService reservations
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _catalog = catalog;
            _requests = requests;
            _history = history;
            _calendars = calendars;
            _reservations = reservations;
        }

        // Who is doing this (user id, IP, trace id) - passed to ReservationService for checks and the audit log.
        private ReservationActor Actor()
        {
            return new(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.TraceIdentifier
            );
        }

        // GET /borrower/home: the student's home page with the equipment catalog.
        [HttpGet("borrower/home")]
        [HttpGet("borrower/dashboard")]
        public async Task<IActionResult> Home(CancellationToken cancellationToken)
        {
            return Page(await _catalog.HomeAsync(RequestActor, cancellationToken), "Home");
        }

        // GET /borrower/reservation: the booking page for one piece of equipment.
        [HttpGet("borrower/reservation")]
        public async Task<IActionResult> Reservation(Guid? equipmentItemId, string? state, Guid[]? id, CancellationToken cancellationToken)
        {
            return Page(await _catalog.ReservationAsync(RequestActor, equipmentItemId, state, id, cancellationToken), "Reservation");
        }

        // WBS 25.00 | Return a free-unit estimate for the selected time range; the save checks again under the item lock.
        [HttpGet("borrower/reservation/availability")]
        public async Task<IActionResult> Availability(Guid equipmentItemId, string? start, string? end, CancellationToken cancellationToken)
        {
            var result = await _catalog.AvailabilityAsync(RequestActor, equipmentItemId, start, end, cancellationToken);
            if (result.Outcome == OperationOutcome.Forbidden)
            {
                return Forbid();
            }

            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            if (!result.Succeeded)
            {
                return BadRequest(new { message = result.Message });
            }

            return Json(
                new
                {
                    bookableUnits = result.Value!.BookableUnits,
                    freeUnits = result.Value.FreeUnits,
                    maxQuantity = result.Value.MaxQuantity,
                }
            );
        }

        // WBS 25.00 | The date picker gets busy times by numbered unit without borrower details.
        [HttpGet("borrower/reservation/schedule")]
        public async Task<IActionResult> Schedule(Guid equipmentItemId, string? from, CancellationToken cancellationToken)
        {
            var result = await _catalog.ScheduleAsync(RequestActor, equipmentItemId, from, cancellationToken);
            if (result.Outcome == OperationOutcome.Forbidden)
            {
                return Forbid();
            }

            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            var schedule = result.Value!;
            return Json(
                new
                {
                    bookableUnits = schedule.BookableUnits,
                    maxPerRequest = schedule.MaxPerRequest,
                    now = schedule.NowLocal,
                    from = schedule.FromLocal,
                    to = schedule.ToLocal,
                    // Fair-use limits, so the picker never offers a time the server would refuse.
                    maxAdvanceMinutes = (int)BorrowingLimits.BookingWindow.TotalMinutes,
                    maxDurationMinutes = (int)BorrowingLimits.MaxBookingDuration.TotalMinutes,
                    busy = schedule.Busy.Select(x => new { unit = x.Unit, start = x.Start, end = x.End }),
                }
            );
        }

        // WBS 25.00 | Bind the posted dates and selected equipment before resolving candidate units.
        [HttpPost("borrower/reservation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReservation(ReservationRequestModel input, CancellationToken cancellationToken)
        {
            var result = await _requests.CreateReservationAsync(RequestActor, input, cancellationToken);
            if (result.Succeeded && result.Value?.CompletedIds.Count > 0)
            {
                return Redirect(
                    $"/borrower/reservation?equipmentItemId={input.EquipmentItemId:D}&state=submitted"
                        + string.Concat(result.Value.CompletedIds.Select(x => $"&id={x:D}"))
                );
            }

            return Page(result, "Reservation");
        }

        // POST /borrower/reservation/{id}/cancel: the student cancels their own pending or approved booking.
        [HttpPost("borrower/reservation/{id:guid}/cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _reservations.CancelAsync(Actor(), id, cancellationToken: cancellationToken);
                TempData["BorrowerFeedback"] =
                    result.Status == ReservationStatus.Expired
                        ? "The pending hold had already expired."
                        : "Reservation cancelled. The equipment slot is available again.";
            }
            catch (ReservationWorkflowException exception)
            {
                if (exception.Code is ReservationErrorCode.Forbidden or ReservationErrorCode.NotFound)
                {
                    return NotFound();
                }

                TempData["BorrowerFeedback"] = exception.Message;
            }

            return Redirect("/borrower/history");
        }

        // GET /borrower/history: the student's past and current bookings, with filters.
        [HttpGet("borrower/history")]
        public async Task<IActionResult> History(
            string? status,
            string? from,
            string? to,
            string? q,
            int page = 1,
            CancellationToken cancellationToken = default
        )
        {
            var result = await _history.HistoryAsync(RequestActor, status, from, to, q, page, cancellationToken);
            if (result.Value is not null)
            {
                result = result with { Value = result.Value with { Feedback = TempData["BorrowerFeedback"] as string } };
            }

            return Page(result, "History");
        }

        // GET /borrower/calendar: month view of the student's bookings.
        [HttpGet("borrower/calendar")]
        public async Task<IActionResult> Calendar(
            int? year,
            int? month,
            Guid? equipmentItemId,
            string? day,
            CancellationToken cancellationToken
        )
        {
            return Page(await _calendars.CalendarAsync(RequestActor, year, month, equipmentItemId, day, cancellationToken), "Calendar");
        }

        // Turns a service result into the right page: the view, a 404, or "access denied".
        private IActionResult Page<T>(OperationResult<T> result, string view)
        {
            if (result.Outcome == OperationOutcome.Forbidden)
            {
                return Forbid();
            }

            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            foreach (var error in result.Errors ?? [])
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            return View(view, result.Value);
        }
    }
}
