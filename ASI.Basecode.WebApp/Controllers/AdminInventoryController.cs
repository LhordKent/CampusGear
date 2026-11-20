using AutoMapper;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// =====================================================================
// ADMIN INVENTORY CONTROLLER  (administrators only)
// The Equipment page: list items, add/edit one (with photo), delete one.
//
// FLOW (save): Add/Edit modal -> Save() -> InventoryService.SaveAsync
// -> EquipmentRepository / AssetCodeRepository -> database (+ photo file on disk).
// =====================================================================
namespace ASI.Basecode.WebApp.Controllers
{
    // WBS 45.00 | The administrator inventory menu leads to routes protected by the Administrator role.
    [Authorize(Roles = RoleNames.Administrator)]
    [AutoValidateAntiforgeryToken]
    public sealed class AdminInventoryController : Mvc.ControllerBase<AdminInventoryController>
    {
        private readonly IInventoryService _inventory;
        private readonly ICategoryService _categories;

        public AdminInventoryController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IInventoryService inventory,
            ICategoryService categories
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _inventory = inventory;
            _categories = categories;
        }

        // GET /admin/equipment: the equipment cards. With ?id=... it also opens the Edit modal for that item.
        [HttpGet("admin/equipment")]
        public async Task<IActionResult> Equipment(string? q, Guid? id, int page = 1, CancellationToken cancellationToken = default)
        {
            var input = new EquipmentEditModel();
            if (id is not null)
            {
                var item = await _inventory.GetEditAsync(id.Value, cancellationToken);
                if (item is null)
                {
                    return NotFound();
                }

                input = new()
                {
                    Id = item.Id,
                    CategoryId = item.CategoryId,
                    Name = item.Name,
                    SerialNumber = item.SerialNumber,
                    Location = item.Location,
                    Description = item.Description,
                    Condition = item.Condition,
                    IsActive = item.IsActive,
                    RowVersion = Convert.ToBase64String(item.RowVersion),
                };
            }

            var model = await PageAsync(input, q, page, cancellationToken);
            return Request.WantsModal() ? PartialView("_EquipmentEditor", model) : View(model);
        }

        // POST /admin/equipment/{id}/delete: deletes the item, or makes it inactive if it has borrowing/repair history.
        [HttpPost("admin/equipment/{id:guid}/delete")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var result = await _inventory.DeleteAsync(RequestActor, id, cancellationToken);
            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            TempData["InventoryFeedback"] = result.Message;
            return Redirect("/admin/equipment");
        }

        // POST /admin/equipment/save: saves the Add/Edit modal (new item gets its asset code here).
        [HttpPost("admin/equipment/save")]
        [RequestSizeLimit(8 * 1024 * 1024)]
        public async Task<IActionResult> Save(
            [Bind(Prefix = "Input")] EquipmentEditModel input,
            IFormFile? imageFile,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid)
            {
                return await InvalidAsync(input, cancellationToken);
            }

            await using var stream = imageFile is { Length: > 0 } ? imageFile.OpenReadStream() : null;
            var result = await _inventory.SaveAsync(
                RequestActor,
                input,
                stream is null ? null : new(stream, imageFile!.Length),
                cancellationToken
            );
            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors ?? [])
                {
                    ModelState.AddModelError(error.Field, error.Message);
                }

                return await InvalidAsync(input, cancellationToken);
            }

            if (Request.IsFetch())
            {
                return ModalForms.Saved(result.Message);
            }

            TempData["InventoryFeedback"] = result.Message;
            return Redirect("/admin/equipment");
        }

        // Save failed: send the errors back to the modal (JSON) or re-show the page.
        private async Task<IActionResult> InvalidAsync(EquipmentEditModel input, CancellationToken ct)
        {
            return Request.IsFetch() ? ModalForms.Rejected(ModelState) : View("Equipment", await PageAsync(input, null, 1, ct));
        }

        // POST /admin/categories: quick "add category" from the equipment page.
        [HttpPost("admin/categories")]
        public async Task<IActionResult> AddCategory(string? name, CancellationToken cancellationToken)
        {
            var result = await _categories.AddFromInventoryAsync(RequestActor, name, cancellationToken);
            TempData["InventoryFeedback"] = result.Message;
            return Redirect("/admin/equipment");
        }

        // Builds everything the Equipment page and its modal need (items, categories, locations, lock state).
        private async Task<InventoryPageModel> PageAsync(EquipmentEditModel input, string? query, int page, CancellationToken ct)
        {
            var result = await _inventory.GetPageAsync(query, page, ct);
            if (input.CategoryId == Guid.Empty)
            {
                input.CategoryId = result.Data.Categories.FirstOrDefault()?.Id ?? Guid.Empty;
            }

            var item = input.Id is { } id ? await _inventory.GetEditAsync(id, ct) : null;
            // Inactive equipment can sit in a deleted category, which the picker otherwise leaves out. Without it the
            // browser would silently select the first category and a save would move the item; listing it keeps the
            // current value, and saving asks for another category.
            IReadOnlyList<ASI.Basecode.Data.Models.CategoryRecord> categories = item?.Category is { IsActive: false } deleted
                ? [deleted, .. result.Data.Categories]
                : result.Data.Categories;
            return new(
                input,
                result.Data.Items,
                categories,
                result.Query,
                result.Data.Page,
                result.Data.Pages,
                result.Data.Total,
                TempData["InventoryFeedback"] as string,
                item?.ImagePath,
                item?.AssetCode,
                result.Data.Locations,
                item is null ? null : await _inventory.GetEditStateAsync(item.Id, ct)
            );
        }
    }
}
