using System.Security.Claims;
using CampusGear.Models;
using CampusGear.Services;
using CampusGear.Services.Reservations;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CampusGear.Controllers;
[Authorize(Roles = "Borrower")]
public sealed class BorrowerController(IBorrowerService borrower, IReservationService reservations) : Controller
{
    private RequestContext ContextActor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    private ReservationActor Actor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier)!, HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    [HttpGet("borrower/home")]
    [HttpGet("borrower/dashboard")]
    public async Task<IActionResult> Home(CancellationToken cancellationToken) =>
        Page(await borrower.HomeAsync(ContextActor(), cancellationToken), "Home");
    [HttpGet("borrower/reservation")]
    public async Task<IActionResult> Reservation(Guid? equipmentItemId, string? state, Guid[]? id, CancellationToken cancellationToken) =>
        Page(await borrower.ReservationAsync(ContextActor(), equipmentItemId, state, id, cancellationToken), "Reservation");
    [HttpGet("borrower/reservation/availability")]
    public async Task<IActionResult> Availability(Guid equipmentItemId, string? start, string? end, CancellationToken cancellationToken)
    {
        var result = await borrower.AvailabilityAsync(ContextActor(), equipmentItemId, start, end, cancellationToken);
        if (result.Outcome == OperationOutcome.Forbidden) return Forbid();
        if (result.Outcome == OperationOutcome.NotFound) return NotFound();
        if (!result.Succeeded) return BadRequest(new { message = result.Message });
        return Json(new { bookableUnits = result.Value!.BookableUnits, freeUnits = result.Value.FreeUnits, maxQuantity = result.Value.MaxQuantity });
    }
    [HttpPost("borrower/reservation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReservation(ReservationInputModel input, CancellationToken cancellationToken)
    {
        var result = await borrower.CreateReservationAsync(ContextActor(), input, cancellationToken);
        if (result.Succeeded && result.Value?.CompletedIds.Count > 0)
            return Redirect($"/borrower/reservation?equipmentItemId={input.EquipmentItemId:D}&state=submitted" +
                string.Concat(result.Value.CompletedIds.Select(x => $"&id={x:D}")));
        return Page(result, "Reservation");
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
    public async Task<IActionResult> History(string? status, string? from, string? to, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await borrower.HistoryAsync(ContextActor(), status, from, to, q, page, cancellationToken);
        if (result.Value is not null) result = result with { Value = result.Value with { Feedback = TempData["BorrowerFeedback"] as string } };
        return Page(result, "History");
    }
    [HttpGet("borrower/calendar")]
    public async Task<IActionResult> Calendar(int? year, int? month, Guid? equipmentItemId, string? day, CancellationToken cancellationToken) =>
        Page(await borrower.CalendarAsync(ContextActor(), year, month, equipmentItemId, day, cancellationToken), "Calendar");
    private IActionResult Page<T>(OperationResult<T> result, string view)
    {
        if (result.Outcome == OperationOutcome.Forbidden) return Forbid();
        if (result.Outcome == OperationOutcome.NotFound) return NotFound();
        foreach (var error in result.Errors ?? []) ModelState.AddModelError(error.Field, error.Message);
        return View(view, result.Value);
    }
}
