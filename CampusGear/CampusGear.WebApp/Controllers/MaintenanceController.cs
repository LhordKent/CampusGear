using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Controllers;

/// <summary>
/// Maintenance cases. The page opens and updates cases in modal dialogs that post here with fetch and get a JSON
/// result back, so a rejected save keeps the dialog open with its message; a plain form post (no JavaScript) still
/// redirects back to the list with the message.
/// </summary>
[Authorize(Roles = "Administrator,Custodian")]
[AutoValidateAntiforgeryToken]
public sealed class MaintenanceController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    TimeProvider clock) : Controller
{
    private const int PageSize = 15;

    [HttpGet("admin/maintenance")]
    public async Task<IActionResult> Index(string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        status = status == "closed" ? "closed" : "open";
        q = q?.Trim();
        if (q?.Length > 120) q = q[..120];
        var all = db.MaintenanceCases.AsNoTracking();
        var openTotal = await all.CountAsync(x => x.Status != MaintenanceStatus.Closed, cancellationToken);
        var closedTotal = await all.CountAsync(x => x.Status == MaintenanceStatus.Closed, cancellationToken);
        var query = status == "closed"
            ? all.Where(x => x.Status == MaintenanceStatus.Closed)
            : all.Where(x => x.Status != MaintenanceStatus.Closed);
        if (!string.IsNullOrEmpty(q))
            query = query.Where(x => x.EquipmentItem.Name.Contains(q) || x.EquipmentItem.AssetCode.Contains(q) ||
                x.Description.Contains(q) || x.Resolution != null && x.Resolution.Contains(q));
        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        var ordered = status == "closed"
            ? query.OrderByDescending(x => x.ClosedAtUtc).ThenByDescending(x => x.OpenedAtUtc)
            : query.OrderByDescending(x => x.OpenedAtUtc);
        var cases = await ordered.Include(x => x.EquipmentItem).Include(x => x.OpenedByUser).Include(x => x.ClosedByUser)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);

        // Every active unit, with the reason it cannot go into maintenance right now (if any), for the Open dialog.
        var inMaintenance = await all.Where(x => x.Status != MaintenanceStatus.Closed)
            .Select(x => x.EquipmentItemId).Distinct().ToListAsync(cancellationToken);
        var onLoan = await db.Loans.AsNoTracking().Where(x => x.ReturnedAtUtc == null)
            .Select(x => x.Reservation.EquipmentItemId).Distinct().ToListAsync(cancellationToken);
        var items = (await db.EquipmentItems.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ThenBy(x => x.AssetCode)
                .Select(x => new { x.Id, x.Name, x.AssetCode, x.Condition }).ToListAsync(cancellationToken))
            .Select(x => new MaintenanceItemOption(x.Id, x.Name, x.AssetCode, x.Condition,
                inMaintenance.Contains(x.Id) ? "already in maintenance" : onLoan.Contains(x.Id) ? "on loan" : null))
            .ToList();

        ViewData["WorkspaceRole"] = User.IsInRole("Administrator") ? "Administrator" : "Custodian";
        return View(new MaintenancePageModel(cases, items, status, q ?? string.Empty, page, pages, total, openTotal, closedTotal,
            TempData["MaintenanceFeedback"] as string, TempData["MaintenanceTone"] as string ?? "success"));
    }

    [HttpPost("admin/maintenance/open")]
    public async Task<IActionResult> Open(Guid equipmentItemId, string? description, CancellationToken cancellationToken)
    {
        description = description?.Trim();
        if (equipmentItemId == Guid.Empty)
            return Result(false, "Choose the equipment that needs maintenance.", "equipmentItemId");
        if (string.IsNullOrWhiteSpace(description))
            return Result(false, "Describe the problem so the repair can be tracked.", "description");
        if (description.Length > 2000)
            return Result(false, "Keep the description to 2000 characters or fewer.", "description");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await SqlWorkflowLocks.AcquireAsync(db, $"CampusGear:EquipmentItem:{equipmentItemId:N}", cancellationToken);
            var item = await db.EquipmentItems.SingleOrDefaultAsync(x => x.Id == equipmentItemId && x.IsActive, cancellationToken);
            if (item is null) return Result(false, "That equipment no longer exists or is inactive. Reload and choose another item.", "equipmentItemId");
            if (await db.Loans.AnyAsync(x => x.Reservation.EquipmentItemId == item.Id && x.ReturnedAtUtc == null, cancellationToken))
                return Result(false, "This item is on loan. Record its return before placing it in maintenance.", "equipmentItemId");
            if (await db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == item.Id && x.Status != MaintenanceStatus.Closed, cancellationToken))
                return Result(false, "This item already has an open maintenance case. Update that case instead.", "equipmentItemId");
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
            return Result(true, $"{item.Name} ({item.AssetCode}) is now in maintenance. New reservations and releases are blocked until the case is closed.");
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or InvalidOperationException)
        { return Result(false, exception is DbUpdateConcurrencyException ? "This record changed. Reload and retry." : exception.Message); }
    }

    [HttpPost("admin/maintenance/{id:guid}/update")]
    public async Task<IActionResult> Update(Guid id, MaintenanceUpdateInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(input.Status) || !Enum.IsDefined(input.Condition))
            return Result(false, "Choose a valid status and equipment condition, and keep the notes to 2000 characters.");
        if (input.Status == MaintenanceStatus.Closed && string.IsNullOrWhiteSpace(input.Resolution))
            return Result(false, "Describe the repair before closing the case.", "Resolution");
        if (input.Status == MaintenanceStatus.Closed && input.Condition is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable)
            return Result(false, "Choose Excellent, Good, or Fair as the condition after repair before closing the case.", "Condition");
        var itemId = await db.MaintenanceCases.AsNoTracking().Where(x => x.Id == id)
            .Select(x => (Guid?)x.EquipmentItemId).SingleOrDefaultAsync(cancellationToken);
        if (itemId is null) return Result(false, "This maintenance case no longer exists. Reload the list.", status: StatusCodes.Status404NotFound);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await SqlWorkflowLocks.AcquireAsync(db, $"CampusGear:EquipmentItem:{itemId.Value:N}", cancellationToken);
            var maintenance = await db.MaintenanceCases.Include(x => x.EquipmentItem).SingleAsync(x => x.Id == id, cancellationToken);
            if (!SqlWorkflowLocks.MatchesVersion(input.RowVersion, maintenance.RowVersion))
                return Result(false, "Someone else changed this case while you were editing. Close this dialog to see the latest details, then try again.");
            if (maintenance.Status == MaintenanceStatus.Closed) return Result(false, "This maintenance case is already closed.");
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
            return Result(true, input.Status == MaintenanceStatus.Closed
                ? $"Repair recorded. {item.Name} ({item.AssetCode}) is back in service in {item.Condition} condition."
                : $"Maintenance saved for {item.Name} ({item.AssetCode}). Status: {(input.Status == MaintenanceStatus.InProgress ? "In progress" : "Open")}.");
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or InvalidOperationException)
        { return Result(false, exception is DbUpdateConcurrencyException ? "This record changed. Reload and retry." : exception.Message); }
    }

    /// <summary>
    /// Answers a dialog's fetch with JSON (422 for a rejected save, naming the field to fix), or a plain form post
    /// with a redirect back to the list carrying the message.
    /// </summary>
    private IActionResult Result(bool ok, string message, string? field = null, int? status = null)
    {
        if (Request.Headers.XRequestedWith == "fetch")
            return new JsonResult(new { ok, message, field }) { StatusCode = status ?? (ok ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity) };
        TempData["MaintenanceFeedback"] = message;
        TempData["MaintenanceTone"] = ok ? "success" : "error";
        return Redirect("/admin/maintenance");
    }
}

public sealed class MaintenanceUpdateInput
{
    public string? RowVersion { get; set; }
    public MaintenanceStatus Status { get; set; }
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;
    [StringLength(2000)] public string? Resolution { get; set; }
}

/// <summary>An equipment unit offered in the Open maintenance dialog; Blocked says why it cannot be chosen.</summary>
public sealed record MaintenanceItemOption(Guid Id, string Name, string AssetCode, EquipmentCondition Condition, string? Blocked);

public sealed record MaintenancePageModel(IReadOnlyList<MaintenanceCase> Cases, IReadOnlyList<MaintenanceItemOption> Items,
    string Status, string Query, int Page, int Pages, int Total, int OpenTotal, int ClosedTotal, string? Feedback, string Tone);
