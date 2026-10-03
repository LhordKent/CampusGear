using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Controllers;

[Authorize(Roles = "Administrator,Custodian")]
[AutoValidateAntiforgeryToken]
public sealed class MaintenanceController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    TimeProvider clock) : Controller
{
    [HttpGet("admin/maintenance")]
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = db.MaintenanceCases.AsNoTracking();
        if (status == "closed") query = query.Where(x => x.Status == MaintenanceStatus.Closed);
        else { status = "open"; query = query.Where(x => x.Status != MaintenanceStatus.Closed); }
        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / 15d));
        page = Math.Clamp(page, 1, pages);
        var cases = await query.Include(x => x.EquipmentItem).OrderByDescending(x => x.OpenedAtUtc)
            .Skip((page - 1) * 15).Take(15).ToListAsync(cancellationToken);
        var items = await db.EquipmentItems.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        ViewData["WorkspaceRole"] = User.IsInRole("Administrator") ? "Administrator" : "Custodian";
        return View(new MaintenancePageModel(cases, items, status, page, pages, TempData["MaintenanceFeedback"] as string));
    }

    [HttpPost("admin/maintenance/open")]
    public async Task<IActionResult> Open(Guid equipmentItemId, string? description, CancellationToken cancellationToken)
    {
        description = description?.Trim();
        if (equipmentItemId == Guid.Empty || string.IsNullOrWhiteSpace(description) || description.Length > 2000)
            return Feedback("Choose equipment and enter a description using 2000 characters or fewer.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await SqlWorkflowLocks.AcquireAsync(db, $"CampusGear:EquipmentItem:{equipmentItemId:N}", cancellationToken);
            var item = await db.EquipmentItems.SingleOrDefaultAsync(x => x.Id == equipmentItemId && x.IsActive, cancellationToken);
            if (item is null) return NotFound();
            if (await db.Loans.AnyAsync(x => x.Reservation.EquipmentItemId == item.Id && x.ReturnedAtUtc == null, cancellationToken))
                return Feedback("Record the active loan's return before placing the item in maintenance.");
            if (await db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == item.Id && x.Status != MaintenanceStatus.Closed, cancellationToken))
                return Feedback("This item already has an open maintenance case.");
            var now = clock.GetUtcNow().UtcDateTime;
            var maintenance = new MaintenanceCase
            {
                EquipmentItemId = item.Id, Description = description, OpenedAtUtc = now,
                OpenedByUserId = users.GetUserId(User)!
            };
            item.IsMaintenanceHold = true;
            item.UpdatedAtUtc = now;
            db.MaintenanceCases.Add(maintenance);
            db.AuditEvents.Add(SqlWorkflowLocks.Audit(HttpContext, users.GetUserId(User)!, "maintenance.opened",
                nameof(MaintenanceCase), maintenance.Id, now, null, JsonSerializer.Serialize(new { maintenance.EquipmentItemId, maintenance.Status })));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Feedback("Maintenance opened. New reservations and releases are blocked until repair is recorded.");
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or InvalidOperationException)
        { return Feedback(exception is DbUpdateConcurrencyException ? "This record changed. Reload and retry." : exception.Message); }
    }

    [HttpPost("admin/maintenance/{id:guid}/update")]
    public async Task<IActionResult> Update(Guid id, MaintenanceUpdateInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(input.Status) || !Enum.IsDefined(input.Condition))
            return Feedback("Choose a valid status and equipment condition.");
        if (input.Status == MaintenanceStatus.Closed &&
            (string.IsNullOrWhiteSpace(input.Resolution) || input.Condition is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable))
            return Feedback("Describe the repair and choose Excellent, Good, or Fair before closing maintenance.");
        var itemId = await db.MaintenanceCases.AsNoTracking().Where(x => x.Id == id)
            .Select(x => (Guid?)x.EquipmentItemId).SingleOrDefaultAsync(cancellationToken);
        if (itemId is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await SqlWorkflowLocks.AcquireAsync(db, $"CampusGear:EquipmentItem:{itemId.Value:N}", cancellationToken);
            var maintenance = await db.MaintenanceCases.Include(x => x.EquipmentItem).SingleAsync(x => x.Id == id, cancellationToken);
            if (!SqlWorkflowLocks.MatchesVersion(input.RowVersion, maintenance.RowVersion))
                return Feedback("This maintenance case changed. Reload before saving.");
            if (maintenance.Status == MaintenanceStatus.Closed) return Feedback("This maintenance case is already closed.");
            var before = maintenance.Status;
            var now = clock.GetUtcNow().UtcDateTime;
            maintenance.Status = input.Status;
            maintenance.Resolution = string.IsNullOrWhiteSpace(input.Resolution) ? null : input.Resolution.Trim();
            var item = maintenance.EquipmentItem;
            if (input.Status == MaintenanceStatus.Closed)
            {
                maintenance.ClosedAtUtc = now;
                maintenance.ClosedByUserId = users.GetUserId(User)!;
                item.Condition = input.Condition;
                item.IsMaintenanceHold = await db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == item.Id && x.Id != id && x.Status != MaintenanceStatus.Closed, cancellationToken);
            }
            item.UpdatedAtUtc = now;
            db.AuditEvents.Add(SqlWorkflowLocks.Audit(HttpContext, users.GetUserId(User)!, "maintenance.updated",
                nameof(MaintenanceCase), id, now, JsonSerializer.Serialize(new { Status = before.ToString() }),
                JsonSerializer.Serialize(new { Status = maintenance.Status.ToString(), item.Condition, item.IsMaintenanceHold })));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Feedback(input.Status == MaintenanceStatus.Closed ? "Repair recorded. Maintenance case closed." : "Maintenance status updated.");
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or InvalidOperationException)
        { return Feedback(exception is DbUpdateConcurrencyException ? "This record changed. Reload and retry." : exception.Message); }
    }

    private RedirectResult Feedback(string message)
    { TempData["MaintenanceFeedback"] = message; return Redirect("/admin/maintenance"); }
}

public sealed class MaintenanceUpdateInput
{
    public string? RowVersion { get; set; }
    public MaintenanceStatus Status { get; set; }
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;
    [StringLength(2000)] public string? Resolution { get; set; }
}
public sealed record MaintenancePageModel(IReadOnlyList<MaintenanceCase> Cases,
    IReadOnlyList<EquipmentItem> Items, string Status, int Page, int Pages, string? Feedback);
