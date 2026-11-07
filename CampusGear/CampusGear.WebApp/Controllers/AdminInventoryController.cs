using System.Security.Claims;
using CampusGear.Services;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CampusGear.Controllers;

[Authorize(Roles = "Administrator")]
[AutoValidateAntiforgeryToken]
public sealed class AdminInventoryController(IInventoryService inventory, ICategoryService categories) : Controller
{
    private RequestContext Actor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    [HttpGet("admin/equipment")]
    public async Task<IActionResult> Equipment(string? q, Guid? id, int page = 1, CancellationToken cancellationToken = default)
    {
        var input = new EquipmentEditInput();
        if (id is not null)
        {
            var item = await inventory.GetEditAsync(id.Value, cancellationToken);
            if (item is null) return NotFound();
            input = new()
            {
                Id = item.Id,
                CategoryId = item.CategoryId,
                AssetCode = item.AssetCode,
                Name = item.Name,
                SerialNumber = item.SerialNumber,
                Location = item.Location,
                Description = item.Description,
                Condition = item.Condition,
                IsActive = item.IsActive,
                RowVersion = Convert.ToBase64String(item.RowVersion),
                ImagePath = item.ImagePath
            };
        }
        return View(await PageAsync(input, q, page, cancellationToken));
    }
    [HttpPost("admin/equipment/{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await inventory.DeleteAsync(Actor(), id, cancellationToken);
        if (result.Outcome == OperationOutcome.NotFound) return NotFound();
        TempData["InventoryFeedback"] = result.Message;
        return Redirect("/admin/equipment");
    }
    [HttpPost("admin/equipment/save")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Save([Bind(Prefix = "Input")] EquipmentEditInput input, IFormFile? imageFile, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Equipment", await PageAsync(input, null, 1, cancellationToken));
        await using var stream = imageFile is { Length: > 0 } ? imageFile.OpenReadStream() : null;
        var result = await inventory.SaveAsync(Actor(), new EquipmentCommand
        {
            Id = input.Id,
            CategoryId = input.CategoryId,
            AssetCode = input.AssetCode,
            Name = input.Name,
            SerialNumber = input.SerialNumber,
            Location = input.Location,
            Description = input.Description,
            Condition = input.Condition,
            IsActive = input.IsActive,
            RowVersion = input.RowVersion,
            RemoveImage = input.RemoveImage
        }, stream is null ? null : new(stream, imageFile!.Length), cancellationToken);
        if (result.Outcome == OperationOutcome.NotFound) return NotFound();
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors ?? []) ModelState.AddModelError(error.Field, error.Message);
            return View("Equipment", await PageAsync(input, null, 1, cancellationToken));
        }
        TempData["InventoryFeedback"] = result.Message;
        return Redirect("/admin/equipment");
    }
    [HttpPost("admin/categories")]
    public async Task<IActionResult> AddCategory(string? name, CancellationToken cancellationToken)
    {
        var result = await categories.AddFromInventoryAsync(Actor(), name, cancellationToken);
        TempData["InventoryFeedback"] = result.Message;
        return Redirect("/admin/equipment");
    }
    private async Task<InventoryPageModel> PageAsync(EquipmentEditInput input, string? query, int page, CancellationToken ct)
    {
        var result = await inventory.GetPageAsync(query, page, ct);
        if (input.CategoryId == Guid.Empty) input.CategoryId = result.Data.Categories.FirstOrDefault()?.Id ?? Guid.Empty;
        if (input.Id is not null && input.ImagePath is null) input.ImagePath = (await inventory.GetEditAsync(input.Id.Value, ct))?.ImagePath;
        return new(input, result.Data.Items, result.Data.Categories, result.Query, result.Data.Page, result.Data.Pages, result.Data.Total, TempData["InventoryFeedback"] as string);
    }
}
