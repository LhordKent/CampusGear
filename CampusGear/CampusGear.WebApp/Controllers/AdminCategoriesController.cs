using System.ComponentModel.DataAnnotations;
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
public sealed class AdminCategoriesController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    TimeProvider clock) : Controller
{
    [HttpGet("admin/categories")]
    public async Task<IActionResult> Index(Guid? id, CancellationToken cancellationToken)
    {
        var input = new CategoryInput();
        if (id is not null)
        {
            var category = await db.EquipmentCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (category is null) return NotFound();
            input = new() { Id = category.Id, Name = category.Name, Description = category.Description,
                IsActive = category.IsActive, RowVersion = Convert.ToBase64String(category.RowVersion) };
        }
        return View(await PageAsync(input, cancellationToken));
    }

    [HttpPost("admin/categories/save")]
    public async Task<IActionResult> Save([Bind(Prefix = "Input")] CategoryInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Index", await PageAsync(input, cancellationToken));
        var id = input.Id ?? Guid.NewGuid();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await SqlWorkflowLocks.AcquireAsync(db, $"CampusGear:EquipmentCategory:{id:N}", cancellationToken);
            var category = input.Id is null ? new EquipmentCategory { Id = id } :
                await db.EquipmentCategories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (category is null) return NotFound();
            if (input.Id is not null && !SqlWorkflowLocks.MatchesVersion(input.RowVersion, category.RowVersion))
                throw new InvalidOperationException("This category changed. Reload before saving.");
            if (!input.IsActive && await db.EquipmentItems.AnyAsync(x => x.CategoryId == id && x.IsActive, cancellationToken))
                throw new InvalidOperationException("Move or deactivate this category's active equipment before deactivating the category.");
            var before = input.Id is null ? null : JsonSerializer.Serialize(new { category.Name, category.Description, category.IsActive });
            category.Name = input.Name.Trim();
            category.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            category.IsActive = input.IsActive;
            category.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            if (input.Id is null) db.EquipmentCategories.Add(category);
            db.AuditEvents.Add(SqlWorkflowLocks.Audit(HttpContext, users.GetUserId(User)!,
                input.Id is null ? "category.created" : "category.updated", nameof(EquipmentCategory), id,
                clock.GetUtcNow().UtcDateTime, before, JsonSerializer.Serialize(new { category.Name, category.Description, category.IsActive })));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            TempData["CategoryFeedback"] = "Category saved.";
            return Redirect("/admin/categories");
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException ||
            exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } })
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            ModelState.AddModelError("", exception is DbUpdateException ? "That category name already exists, or the record changed." : exception.Message);
            return View("Index", await PageAsync(input, cancellationToken));
        }
    }

    private async Task<CategoriesPageModel> PageAsync(CategoryInput input, CancellationToken cancellationToken) => new(input,
        await db.EquipmentCategories.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new CategoryRow(x.Id, x.Name, x.Description, x.IsActive, x.Items.Count))
            .ToListAsync(cancellationToken), TempData["CategoryFeedback"] as string);
}
public sealed class CategoryInput
{
    public Guid? Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [StringLength(1000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RowVersion { get; set; }
}
public sealed record CategoryRow(Guid Id, string Name, string? Description, bool IsActive, int Items);
public sealed record CategoriesPageModel(CategoryInput Input, IReadOnlyList<CategoryRow> Categories, string? Feedback);
