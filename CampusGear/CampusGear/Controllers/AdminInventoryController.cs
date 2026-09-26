using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Controllers;

[Authorize(Roles = "Administrator")]
[AutoValidateAntiforgeryToken]
public sealed class AdminInventoryController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    TimeProvider clock) : Controller
{
    [HttpGet("admin/equipment")]
    public async Task<IActionResult> Equipment(string? q, Guid? id, int page = 1, CancellationToken cancellationToken = default)
    {
        var input = new EquipmentEditInput();
        if (id is not null)
        {
            var item = await db.EquipmentItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (item is null) return NotFound();
            input = new EquipmentEditInput
            {
                Id = item.Id, CategoryId = item.CategoryId, AssetCode = item.AssetCode, Name = item.Name,
                SerialNumber = item.SerialNumber, Location = item.Location, Description = item.Description,
                Condition = item.Condition, IsActive = item.IsActive, RowVersion = Convert.ToBase64String(item.RowVersion)
            };
        }
        return View(await BuildPageAsync(input, q, page, cancellationToken));
    }

    [HttpPost("admin/equipment/save")]
    public async Task<IActionResult> Save([Bind(Prefix = "Input")] EquipmentEditInput input,
        CancellationToken cancellationToken)
    {
        input.AssetCode = input.AssetCode?.Trim() ?? "";
        input.Name = input.Name?.Trim() ?? "";
        if (!Enum.IsDefined(input.Condition)) ModelState.AddModelError("Input.Condition", "Choose a valid condition.");
        if (!await db.EquipmentCategories.AnyAsync(x => x.Id == input.CategoryId && x.IsActive, cancellationToken))
            ModelState.AddModelError("Input.CategoryId", "Choose an active category.");
        if (!ModelState.IsValid) return View("Equipment", await BuildPageAsync(input, null, 1, cancellationToken));

        var itemId = input.Id ?? Guid.NewGuid();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await SqlWorkflowLocks.AcquireAsync(db, $"CampusGear:EquipmentItem:{itemId:N}", cancellationToken);
            await SqlWorkflowLocks.AcquireAsync(db, $"CampusGear:EquipmentCategory:{input.CategoryId:N}", cancellationToken);
            if (!await db.EquipmentCategories.AsNoTracking().AnyAsync(x => x.Id == input.CategoryId && x.IsActive, cancellationToken))
                throw new InvalidOperationException("This category is inactive. Choose another category.");
            var item = input.Id is null ? new EquipmentItem { Id = itemId } :
                await db.EquipmentItems.SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
            if (item is null) return NotFound();
            if (input.Id is not null && !SqlWorkflowLocks.MatchesVersion(input.RowVersion, item.RowVersion))
                throw new InvalidOperationException("This equipment changed. Reload it before saving.");
            if (await db.Loans.AnyAsync(x => x.Reservation.EquipmentItemId == itemId && x.ReturnedAtUtc == null, cancellationToken))
                throw new InvalidOperationException("Record the active loan's return before editing this equipment.");
            var now = clock.GetUtcNow().UtcDateTime;
            if (!input.IsActive && await db.Reservations.AnyAsync(x => x.EquipmentItemId == itemId &&
                (x.Status == ReservationStatus.Approved || x.Status == ReservationStatus.Pending && x.HoldExpiresAtUtc > now), cancellationToken))
                throw new InvalidOperationException("Resolve this item's pending and approved reservations before deactivating it.");

            var before = input.Id is null ? null : JsonSerializer.Serialize(new { item.AssetCode, item.Name, item.Condition, item.IsActive });
            item.CategoryId = input.CategoryId;
            item.AssetCode = input.AssetCode;
            item.Name = input.Name;
            item.SerialNumber = Clean(input.SerialNumber);
            item.Location = Clean(input.Location);
            item.Description = Clean(input.Description);
            item.Condition = input.Condition;
            item.IsActive = input.IsActive;
            item.UpdatedAtUtc = now;
            if (input.Id is null) db.EquipmentItems.Add(item);
            if (input.Condition is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable)
            {
                item.IsMaintenanceHold = true;
                if (!await db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == itemId && x.Status != MaintenanceStatus.Closed, cancellationToken))
                    db.MaintenanceCases.Add(new MaintenanceCase
                    {
                        EquipmentItemId = itemId, Description = $"Inventory condition recorded as {input.Condition}.",
                        OpenedByUserId = users.GetUserId(User)!, OpenedAtUtc = now
                    });
            }
            // Open maintenance can only be cleared by recording its resolution.
            if (await db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == itemId && x.Status != MaintenanceStatus.Closed, cancellationToken))
                item.IsMaintenanceHold = true;
            db.AuditEvents.Add(SqlWorkflowLocks.Audit(HttpContext, users.GetUserId(User)!,
                input.Id is null ? "equipment.created" : "equipment.updated", nameof(EquipmentItem), itemId, now,
                before, JsonSerializer.Serialize(new { item.AssetCode, item.Name, item.Condition, item.IsActive, item.IsMaintenanceHold })));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            TempData["InventoryFeedback"] = input.Id is null ? "Equipment added." : "Equipment updated.";
            return Redirect("/admin/equipment");
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException ||
            exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } })
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", exception is DbUpdateException
                ? "The asset code or serial number already exists, or the record changed. Reload and try again."
                : exception.Message);
            return View("Equipment", await BuildPageAsync(input, null, 1, cancellationToken));
        }
    }

    [HttpPost("admin/categories")]
    public async Task<IActionResult> AddCategory(string? name, CancellationToken cancellationToken)
    {
        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            TempData["InventoryFeedback"] = "Enter a category name using 120 characters or fewer.";
            return Redirect("/admin/equipment");
        }
        var category = new EquipmentCategory { Name = name };
        db.EquipmentCategories.Add(category);
        db.AuditEvents.Add(SqlWorkflowLocks.Audit(HttpContext, users.GetUserId(User)!, "category.created",
            nameof(EquipmentCategory), category.Id, clock.GetUtcNow().UtcDateTime, null, JsonSerializer.Serialize(new { name })));
        try { await db.SaveChangesAsync(cancellationToken); TempData["InventoryFeedback"] = "Category added."; }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { TempData["InventoryFeedback"] = "That category already exists."; }
        return Redirect("/admin/equipment");
    }

    private async Task<InventoryPageModel> BuildPageAsync(EquipmentEditInput input, string? q, int page, CancellationToken cancellationToken)
    {
        var query = db.EquipmentItems.AsNoTracking();
        q = q?.Trim();
        if (!string.IsNullOrEmpty(q)) query = query.Where(x => x.Name.Contains(q) || x.AssetCode.Contains(q));
        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / 15d));
        page = Math.Clamp(page, 1, pages);
        var items = await query.Include(x => x.Category).OrderBy(x => x.AssetCode).Skip((page - 1) * 15).Take(15).ToListAsync(cancellationToken);
        var categories = await db.EquipmentCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        if (input.CategoryId == Guid.Empty) input.CategoryId = categories.FirstOrDefault()?.Id ?? Guid.Empty;
        return new(input, items, categories, q ?? "", page, pages, total, TempData["InventoryFeedback"] as string);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

public sealed class EquipmentEditInput
{
    public Guid? Id { get; set; }
    public Guid CategoryId { get; set; }
    [Required, StringLength(64)] public string AssetCode { get; set; } = "";
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [StringLength(120)] public string? SerialNumber { get; set; }
    [StringLength(160)] public string? Location { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;
    public bool IsActive { get; set; } = true;
    public string? RowVersion { get; set; }
}
public sealed record InventoryPageModel(EquipmentEditInput Input, IReadOnlyList<EquipmentItem> Items,
    IReadOnlyList<EquipmentCategory> Categories, string Query, int Page, int Pages, int Total, string? Feedback);

internal static class SqlWorkflowLocks
{
    internal static async Task AcquireAsync(ApplicationDbContext db, string resource, CancellationToken cancellationToken)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var key = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource };
        await db.Database.ExecuteSqlRawAsync("EXEC @result = sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000",
            new object[] { result, key }, cancellationToken);
        if (result.Value is not int code || code < 0) throw new InvalidOperationException("This record is busy. Please retry.");
    }
    internal static bool MatchesVersion(string? encoded, byte[] current)
    {
        if (encoded is null) return false;
        try { return Convert.FromBase64String(encoded).SequenceEqual(current); }
        catch (FormatException) { return false; }
    }
    internal static AuditEvent Audit(HttpContext context, string actor, string action, string type,
        Guid id, DateTime now, string? before, string? after) => new()
    {
        ActorUserId = actor, Action = action, EntityType = type, EntityId = id.ToString(), Outcome = "Succeeded",
        BeforeJson = before, AfterJson = after, IpAddress = context.Connection.RemoteIpAddress?.ToString(),
        CorrelationId = context.TraceIdentifier, CreatedAtUtc = now
    };
}
