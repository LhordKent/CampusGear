using System.Security.Claims;
using CampusGear.Services;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusGear.Controllers;

[Authorize(Roles = "Administrator")]
[AutoValidateAntiforgeryToken]
public sealed class AdminCategoriesController(ICategoryService categories) : Controller
{
    private RequestContext Actor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier),
        HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);

    [HttpGet("admin/categories")]
    public async Task<IActionResult> Index(Guid? id, string? q, CancellationToken cancellationToken)
    {
        var input = new CategoryInput();
        if (id is not null)
        {
            var x = await categories.GetEditAsync(id.Value, cancellationToken);
            if (x is null) return NotFound();
            input = new() { Id = x.Id, Name = x.Name, Description = x.Description,
                IsActive = x.IsActive, RowVersion = x.RowVersion };
        }
        return View(await PageAsync(input, cancellationToken, q));
    }

    [HttpPost("admin/categories/{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await categories.DeleteAsync(Actor(), id, cancellationToken);
        if (result.Outcome == OperationOutcome.NotFound) return NotFound();
        TempData["CategoryFeedback"] = result.Message;
        return Redirect("/admin/categories");
    }

    [HttpPost("admin/categories/save")]
    public async Task<IActionResult> Save([Bind(Prefix = "Input")] CategoryInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Index", await PageAsync(input, cancellationToken));
        var result = await categories.SaveAsync(Actor(),
            new(input.Id, input.Name, input.Description, input.IsActive, input.RowVersion), cancellationToken);
        if (result.Outcome == OperationOutcome.NotFound) return NotFound();
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors ?? []) ModelState.AddModelError(error.Field, error.Message);
            return View("Index", await PageAsync(input, cancellationToken));
        }
        TempData["CategoryFeedback"] = result.Message;
        return Redirect("/admin/categories");
    }

    private async Task<CategoriesPageModel> PageAsync(CategoryInput input, CancellationToken ct, string? q = null)
    {
        var page = await categories.GetPageAsync(q, ct);
        return new(input, page.Categories.Select(x => new CategoryRow(x.Id, x.Name, x.Description, x.IsActive, x.Items)).ToList(),
            TempData["CategoryFeedback"] as string, page.Query, page.Total);
    }
}
