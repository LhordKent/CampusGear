using System.Security.Claims;
using AutoMapper;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// =====================================================================
// CUSTODIAN CONTROLLER  (the borrowing desk)
// Booking life cycle:  Pending -> Approve -> Approved -> Release (item handed over) -> Released (on loan)
// -> Return (item back) -> Completed.   Reject / Cancel / Expire end it early.
//
// FLOW: button on the queue -> this controller -> ReservationService (ApproveAsync, RejectAsync,
// ReleaseAsync, ReturnAsync) -> database. TransitionAsync() below runs all four the same way.
// =====================================================================
namespace ASI.Basecode.WebApp.Controllers
{
    // The borrowing desk belongs to the custodian. Administrators get read-only access to history, the calendar and
    // reservation details; every decision, release and return is Custodian-only. Each action states its roles, because
    // stacked [Authorize] attributes must all pass.
    [Authorize]
    [AutoValidateAntiforgeryToken]
    public sealed class CustodianController : Mvc.ControllerBase<CustodianController>
    {
        private readonly ICustodianQueueService _desk;
        private readonly IReservationService _reservations;
        private readonly IAvailabilityCalendarService _calendars;

        public CustodianController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            ICustodianQueueService desk,
            IReservationService reservations,
            IAvailabilityCalendarService calendars
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _desk = desk;
            _reservations = reservations;
            _calendars = calendars;
        }

        private const int PageSize = 12;

        // GET /custodian/approvals: pending requests to approve/reject, and approved bookings ready to release.
        [Authorize(Roles = RoleNames.Custodian)]
        [HttpGet("custodian/approvals")]
        public async Task<IActionResult> Approvals(
            string? q,
            string? status,
            string? from,
            string? to,
            int page = 1,
            CancellationToken cancellationToken = default
        )
        {
            Workspace();
            return View("Queue", Feedback(await _desk.GetApprovalQueueAsync(new(q, status, from, to, page), cancellationToken)));
        }

        // GET /custodian/returns: items currently on loan, waiting to be returned.
        [Authorize(Roles = RoleNames.Custodian)]
        [HttpGet("custodian/returns")]
        public async Task<IActionResult> Returns(
            string? q,
            string? queue,
            string? from,
            string? to,
            int page = 1,
            CancellationToken cancellationToken = default
        )
        {
            // Approved bookings are released from Approval & Release; old links to the release tab go there.
            if (queue?.ToLowerInvariant() == "release")
            {
                return Redirect("/custodian/approvals?status=approved");
            }

            Workspace();
            return View("Queue", Feedback(await _desk.GetReturnQueueAsync(new(q, "all", from, to, page), cancellationToken)));
        }

        // GET /custodian/history: every booking, finished or not, with filters.
        [Authorize(Roles = RoleNames.Custodian)]
        [HttpGet("custodian/history")]
        public async Task<IActionResult> History(
            string? q,
            string? status,
            string? from,
            string? to,
            int page = 1,
            CancellationToken cancellationToken = default
        )
        {
            Workspace();
            return View("Queue", Feedback(await _desk.GetHistoryAsync(new(q, status, from, to, page), cancellationToken)));
        }

        // GET /custodian/reservations/{id}: full details of one booking.
        [Authorize(Roles = RoleNames.CustodianOrAdministrator)]
        [HttpGet("custodian/reservations/{id:guid}")]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            Workspace();
            var model = await _desk.GetDetailsAsync(id, User.IsInRole(RoleNames.Custodian), cancellationToken);
            if (model is null)
            {
                return NotFound();
            }

            return View(model with { Feedback = TempData["CustodianFeedback"] as string, Error = TempData["CustodianError"] as string });
        }

        // POST .../approve: Pending -> Approved.
        [Authorize(Roles = RoleNames.Custodian)]
        [HttpPost("custodian/reservations/{id:guid}/approve")]
        public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
        {
            return TransitionAsync(
                id,
                actor => _reservations.ApproveAsync(actor, id, cancellationToken),
                "Reservation approved. The booking is pending release at its start time.",
                successRedirect: "/custodian/approvals"
            );
        }

        // POST .../reject: Pending -> Rejected (a reason is required).
        [Authorize(Roles = RoleNames.Custodian)]
        [HttpPost("custodian/reservations/{id:guid}/reject")]
        public Task<IActionResult> Reject(Guid id, [FromForm] string? reason, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            {
                return InvalidFormAsync(id, "Enter a rejection reason using 1 to 1000 characters.");
            }

            return TransitionAsync(
                id,
                actor => _reservations.RejectAsync(actor, id, reason.Trim(), cancellationToken),
                "Request rejected. Its equipment slot is available again."
            );
        }

        // POST .../release: hands the item over. Approved -> Released, and a Loan record is created.
        [Authorize(Roles = RoleNames.Custodian)]
        [HttpPost("custodian/reservations/{id:guid}/release")]
        public Task<IActionResult> Release(
            Guid id,
            [FromForm] string? condition,
            [FromForm] string? notes,
            CancellationToken cancellationToken
        )
        {
            if (!ValidCondition(condition, out var parsed) || parsed is EquipmentCondition.Damaged)
            {
                return InvalidFormAsync(id, "Choose Excellent, Good, or Fair for an equipment release.");
            }

            if (notes?.Length > 2000)
            {
                return InvalidFormAsync(id, "Release notes must use 2000 characters or fewer.");
            }

            return TransitionAsync(
                id,
                actor => _reservations.ReleaseAsync(actor, id, parsed, notes, cancellationToken),
                "Release recorded. The equipment is now on loan."
            );
        }

        // POST .../return: takes the item back. Released -> Completed. A Damaged return opens a maintenance case.
        [Authorize(Roles = RoleNames.Custodian)]
        [HttpPost("custodian/reservations/{id:guid}/return")]
        public Task<IActionResult> RecordReturn(
            Guid id,
            [FromForm] string? condition,
            [FromForm] string? notes,
            CancellationToken cancellationToken
        )
        {
            if (!ValidCondition(condition, out var parsed))
            {
                return InvalidFormAsync(id, "Choose a valid return condition.");
            }

            if (notes?.Length > 2000)
            {
                return InvalidFormAsync(id, "Return notes must use 2000 characters or fewer.");
            }

            return TransitionAsync(
                id,
                actor => _reservations.ReturnAsync(actor, id, parsed, notes, cancellationToken),
                "Return recorded. The equipment condition and borrowing history are updated.",
                returnTransition: true
            );
        }

        // GET /custodian/calendar: month view of all bookings.
        [Authorize(Roles = RoleNames.Custodian)]
        [HttpGet("custodian/calendar")]
        public async Task<IActionResult> Calendar(
            int? year,
            int? month,
            Guid? equipmentItemId,
            string? day,
            CancellationToken cancellationToken
        )
        {
            Workspace();
            return View("Calendar", await _calendars.GetStaffCalendarAsync(year, month, equipmentItemId, day, cancellationToken));
        }

        // GET /admin/history: the same history page, for administrators (read-only).
        [Authorize(Roles = RoleNames.Administrator)]
        [HttpGet("admin/history")]
        public Task<IActionResult> AdminHistory(
            string? q,
            string? status,
            string? from,
            string? to,
            int page = 1,
            CancellationToken cancellationToken = default
        )
        {
            return History(q, status, from, to, page, cancellationToken);
        }

        // GET /admin/calendar: the same calendar, for administrators (read-only).
        [Authorize(Roles = RoleNames.Administrator)]
        [HttpGet("admin/calendar")]
        public Task<IActionResult> AdminCalendar(
            int? year,
            int? month,
            Guid? equipmentItemId,
            string? day,
            CancellationToken cancellationToken
        )
        {
            return Calendar(year, month, equipmentItemId, day, cancellationToken);
        }

        private CustodianListViewModel Feedback(CustodianListViewModel model)
        {
            return model with
            {
                Feedback = TempData["CustodianFeedback"] as string,
                Error = model.Error ?? TempData["CustodianError"] as string,
            };
        }

        // Runs one booking action (approve/reject/release/return) and turns the result or error into
        // a toast (modal form) or a message + redirect (normal form).
        private async Task<IActionResult> TransitionAsync(
            Guid id,
            Func<ReservationActor, Task<ReservationResult>> operation,
            string success,
            bool returnTransition = false,
            string? successRedirect = null
        )
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
            {
                return Forbid();
            }

            try
            {
                var result = await operation(new(userId, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier));
                var message =
                    result.Status == ReservationStatus.Expired
                        ? "The pending hold expired before this action. Its equipment slot was released."
                    : returnTransition && result.ConditionAtReturn is EquipmentCondition.Damaged
                        ? "Return recorded. A maintenance hold was created for this equipment."
                    : success;

                // A modal form (js/modal-forms.js) shows the message as a toast and refreshes the list in place.
                if (Request.IsFetch())
                {
                    return ModalForms.Saved(message);
                }

                TempData["CustodianFeedback"] = message;
                if (successRedirect is not null)
                {
                    return Redirect(successRedirect);
                }
            }
            catch (ReservationWorkflowException exception)
            {
                if (Request.IsFetch())
                {
                    var status = exception.Code switch
                    {
                        ReservationErrorCode.Forbidden => StatusCodes.Status403Forbidden,
                        ReservationErrorCode.NotFound => StatusCodes.Status404NotFound,
                        _ => StatusCodes.Status422UnprocessableEntity,
                    };
                    var text = exception.Code == ReservationErrorCode.NotFound ? "This reservation no longer exists." : exception.Message;
                    return new JsonResult(ApiResult.CreateError(text, null)) { StatusCode = status };
                }

                if (exception.Code == ReservationErrorCode.Forbidden)
                {
                    return Forbid();
                }

                if (exception.Code == ReservationErrorCode.NotFound)
                {
                    return NotFound();
                }

                TempData["CustodianError"] = exception.Message;
            }

            return Redirect($"/custodian/reservations/{id:D}");
        }

        // A bad form value (e.g. no condition picked): show the error without calling the service.
        private Task<IActionResult> InvalidFormAsync(Guid id, string error)
        {
            if (Request.IsFetch())
            {
                return Task.FromResult<IActionResult>(new JsonResult(ApiResult.CreateError(error, null)) { StatusCode = StatusCodes.Status422UnprocessableEntity });
            }

            TempData["CustodianError"] = error;
            return Task.FromResult<IActionResult>(Redirect($"/custodian/reservations/{id:D}"));
        }

        // Reads the condition text from the form ("Good", "Damaged", ...).
        private static bool ValidCondition(string? value, out EquipmentCondition condition)
        {
            return Enum.TryParse(value, true, out condition) && Enum.IsDefined(condition);
        }

        private void Workspace()
        {
            ViewData["WorkspaceRole"] = User.IsInRole(RoleNames.Administrator) ? RoleNames.Administrator : RoleNames.Custodian;
        }
    }
}
