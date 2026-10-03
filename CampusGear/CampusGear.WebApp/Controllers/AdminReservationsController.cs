using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CampusGear.Data;
using CampusGear.Models;
using CampusGear.Services.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Controllers;

[Authorize(Roles = "Administrator")]
[AutoValidateAntiforgeryToken]
public sealed class AdminReservationsController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    IReservationService reservations, TimeProvider clock) : Controller
{
    [HttpGet("admin/reservations")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var localNow = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Manila);
        var start = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0).AddHours(1);
        return View(await PageAsync(new AdminReservationInput
        {
            RequestId = Guid.NewGuid(), StartLocal = start.ToString("yyyy-MM-ddTHH:mm"),
            EndLocal = start.AddHours(2).ToString("yyyy-MM-ddTHH:mm")
        }, cancellationToken));
    }

    [HttpPost("admin/reservations")]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] AdminReservationInput input, CancellationToken cancellationToken)
    {
        if (input.RequestId == Guid.Empty) ModelState.AddModelError("", "Reload this form before submitting.");
        var start = Parse(input.StartLocal, "Input.StartLocal");
        var end = Parse(input.EndLocal, "Input.EndLocal");
        if (start is not null && end is not null && (start <= clock.GetUtcNow().UtcDateTime || start >= end))
            ModelState.AddModelError("", "Choose a future start time and a later end time.");
        if (!ModelState.IsValid) return View("Index", await PageAsync(input, cancellationToken));
        try
        {
            var result = await reservations.CreateAsync(new ReservationActor(users.GetUserId(User)!,
                HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier),
                new CreateReservationCommand(input.RequestId, input.BorrowerUserId, input.EquipmentItemId,
                    start!.Value, end!.Value, input.Purpose, input.Notes), cancellationToken);
            TempData["AdminReservationFeedback"] = "Request saved for the selected borrower. It is pending staff approval.";
            return Redirect($"/admin/approvals?status=pending");
        }
        catch (ReservationWorkflowException exception)
        {
            if (exception.Code == ReservationErrorCode.Forbidden) return Forbid();
            ModelState.AddModelError("", exception.Message);
            return View("Index", await PageAsync(input, cancellationToken));
        }
    }

    [HttpPost("admin/reservations/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            await reservations.CancelAsync(new ReservationActor(users.GetUserId(User)!,
                HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier), id, reason, cancellationToken);
            TempData["AdminReservationFeedback"] = "Reservation cancelled.";
        }
        catch (ReservationWorkflowException exception)
        {
            if (exception.Code is ReservationErrorCode.Forbidden or ReservationErrorCode.NotFound) return NotFound();
            TempData["AdminReservationFeedback"] = exception.Message;
        }
        return Redirect("/admin/reservations");
    }

    private async Task<AdminReservationsPage> PageAsync(AdminReservationInput input, CancellationToken cancellationToken)
    {
        var borrowers = await db.BorrowerProfiles.AsNoTracking()
            .Where(x => x.IsEligible && x.User.IsActive && x.User.EmailConfirmed &&
                db.UserRoles.Any(ur => ur.UserId == x.UserId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Borrower")))
            .OrderBy(x => x.User.FullName).Select(x => new AdminBorrowerChoice(x.UserId, x.User.FullName, x.User.Email!))
            .ToListAsync(cancellationToken);
        var items = await db.EquipmentItems.AsNoTracking().Where(x => x.IsActive && !x.IsMaintenanceHold &&
            x.Condition != EquipmentCondition.Damaged && x.Condition != EquipmentCondition.Unserviceable)
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var recent = await db.Reservations.AsNoTracking().Include(x => x.EquipmentItem)
            .Include(x => x.BorrowerProfile).ThenInclude(x => x.User)
            .OrderByDescending(x => x.CreatedAtUtc).Take(20).ToListAsync(cancellationToken);
        return new(input, borrowers, items, recent, TempData["AdminReservationFeedback"] as string);
    }

    private DateTime? Parse(string? text, string field)
    {
        if (!DateTime.TryParseExact(text, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Year is < 2000 or > 2100)
        { ModelState.AddModelError(field, "Choose a valid date and time in Manila."); return null; }
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), Manila);
    }
    private static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
}

public sealed class AdminReservationInput
{
    public Guid RequestId { get; set; }
    [Required] public string BorrowerUserId { get; set; } = "";
    public Guid EquipmentItemId { get; set; }
    [Required] public string StartLocal { get; set; } = "";
    [Required] public string EndLocal { get; set; } = "";
    [Required, StringLength(500)] public string Purpose { get; set; } = "";
    [StringLength(2000)] public string? Notes { get; set; }
}
public sealed record AdminBorrowerChoice(string UserId, string FullName, string Email);
public sealed record AdminReservationsPage(AdminReservationInput Input, IReadOnlyList<AdminBorrowerChoice> Borrowers,
    IReadOnlyList<EquipmentItem> Items, IReadOnlyList<Reservation> Recent, string? Feedback);
