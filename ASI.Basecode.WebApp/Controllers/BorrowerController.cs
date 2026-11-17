using System.Security.Claims;
using AutoMapper;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
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

        private ReservationActor Actor()
        {
            return new(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.TraceIdentifier
            );
        }

        [HttpGet("borrower/home")]
        [HttpGet("borrower/dashboard")]
        public async Task<IActionResult> Home(CancellationToken cancellationToken)
        {
            return Page(await _catalog.HomeAsync(RequestActor, cancellationToken), "Home");
        }

        [HttpGet("borrower/reservation")]
        public async Task<IActionResult> Reservation(Guid? equipmentItemId, string? state, Guid[]? id, CancellationToken cancellationToken)
        {
            return Page(await _catalog.ReservationAsync(RequestActor, equipmentItemId, state, id, cancellationToken), "Reservation");
        }

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

        // Busy times for the request dialog's date picker. No borrower details are included, only numbered units.
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
