using System.Globalization;
using CampusGear.Services;
using System.Security.Claims;
using CampusGear.Models;
using CampusGear.Services.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusGear.Controllers;

// The borrowing desk belongs to the custodian. Administrators get read-only access to history, the calendar and
// reservation details; every decision, release and return is Custodian-only. Each action states its roles, because
// stacked [Authorize] attributes must all pass.
[Authorize]
[AutoValidateAntiforgeryToken]
public sealed class CustodianController(
    ICustodianService desk,
    IReservationService reservations) : Controller
{
    private const int PageSize = 12;

    [Authorize(Roles = "Custodian")]
    [HttpGet("custodian/approvals")]
    public async Task<IActionResult> Approvals(string? q, string? status, string? from, string? to,
        int page = 1, CancellationToken cancellationToken = default)
    {
        Workspace();
        return View("Queue", Feedback(await desk.GetListAsync(nameof(Approvals), "Reservation approvals", q, status, from, to, page, "approvals", cancellationToken)));
    }

    [Authorize(Roles = "Custodian")]
    [HttpGet("custodian/returns")]
    public async Task<IActionResult> Returns(string? q, string? queue, string? from, string? to,
        int page = 1, CancellationToken cancellationToken = default)
    {
        Workspace();
        queue = queue?.ToLowerInvariant() == "release" ? "release" : "active";
        return View("Queue", Feedback(await desk.GetListAsync(nameof(Returns), "Release and return desk", q, "all", from, to, page, queue, cancellationToken)));
    }

    [Authorize(Roles = "Custodian")]
    [HttpGet("custodian/history")]
    public async Task<IActionResult> History(string? q, string? status, string? from, string? to,
        int page = 1, CancellationToken cancellationToken = default)
    {
        Workspace();
        return View("Queue", Feedback(await desk.GetListAsync(User.IsInRole("Administrator") ? nameof(AdminHistory) : nameof(History),
            "Borrowing history", q, status, from, to, page, "history", cancellationToken)));
    }

    [Authorize(Roles = "Custodian,Administrator")]
    [HttpGet("custodian/reservations/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        Workspace();
        var model = await desk.GetDetailsAsync(id, User.IsInRole("Custodian"), cancellationToken);
        if (model is null) return NotFound();
        return View(model with { Feedback = TempData["CustodianFeedback"] as string, Error = TempData["CustodianError"] as string });
    }

    [Authorize(Roles = "Custodian")]
    [HttpPost("custodian/reservations/{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, actor => reservations.ApproveAsync(actor, id, cancellationToken),
            "Reservation approved. The booking is ready for release at its start time.");

    [Authorize(Roles = "Custodian")]
    [HttpPost("custodian/reservations/{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, [FromForm] string? reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            return InvalidFormAsync(id, "Enter a rejection reason using 1 to 1000 characters.");
        return TransitionAsync(id, actor => reservations.RejectAsync(actor, id, reason.Trim(), cancellationToken),
            "Request rejected. Its equipment slot is available again.");
    }

    [Authorize(Roles = "Custodian")]
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

    [Authorize(Roles = "Custodian")]
    [HttpPost("custodian/reservations/{id:guid}/return")]
    public Task<IActionResult> RecordReturn(Guid id, [FromForm] string? condition, [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        if (!ValidCondition(condition, out var parsed)) return InvalidFormAsync(id, "Choose a valid return condition.");
        if (notes?.Length > 2000) return InvalidFormAsync(id, "Return notes must use 2000 characters or fewer.");
        return TransitionAsync(id, actor => reservations.ReturnAsync(actor, id, parsed, notes, cancellationToken),
            "Return recorded. The equipment condition and borrowing history are updated.", returnTransition: true);
    }

    [Authorize(Roles = "Custodian")]
    [HttpGet("custodian/calendar")]
    public async Task<IActionResult> Calendar(int? year, int? month, Guid? equipmentItemId, string? day,
        CancellationToken cancellationToken)
    {
        Workspace();
        return View("Calendar", await desk.GetCalendarAsync(year, month, equipmentItemId, day, cancellationToken));
    }

    [Authorize(Roles = "Administrator")]
    [HttpGet("admin/history")]
    public Task<IActionResult> AdminHistory(string? q, string? status, string? from, string? to,
        int page = 1, CancellationToken cancellationToken = default) => History(q, status, from, to, page, cancellationToken);

    [Authorize(Roles = "Administrator")]
    [HttpGet("admin/calendar")]
    public Task<IActionResult> AdminCalendar(int? year, int? month, Guid? equipmentItemId, string? day,
        CancellationToken cancellationToken) => Calendar(year, month, equipmentItemId, day, cancellationToken);

    private CustodianListViewModel Feedback(CustodianListViewModel model) => model with
    {
        Feedback = TempData["CustodianFeedback"] as string,
        Error = model.Error ?? TempData["CustodianError"] as string
    };

    private async Task<IActionResult> TransitionAsync(Guid id,
        Func<ReservationActor, Task<ReservationResult>> operation, string success, bool returnTransition = false)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
