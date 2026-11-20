using AutoMapper;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// =====================================================================
// ADMIN CATEGORIES CONTROLLER  (administrators only)
// The Equipment Categories page: list, add/edit (modal), delete.
// FLOW: page or modal -> this controller -> CategoryService -> CategoryRepository -> database.
// =====================================================================
namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = RoleNames.Administrator)]
    [AutoValidateAntiforgeryToken]
    public sealed class AdminCategoriesController : Mvc.ControllerBase<AdminCategoriesController>
    {
        private readonly ICategoryService _categories;

        public AdminCategoriesController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            ICategoryService categories
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _categories = categories;
        }

        // GET /admin/categories: the category list. With ?id=... it also opens the Edit modal.
        [HttpGet("admin/categories")]
        public async Task<IActionResult> Index(Guid? id, string? q, CancellationToken cancellationToken)
        {
            var input = new CategoryEditModel();
            if (id is not null)
            {
                var x = await _categories.GetEditAsync(id.Value, cancellationToken);
                if (x is null)
                {
                    return NotFound();
                }

                input = x;
            }

            var page = await PageAsync(input, cancellationToken, q);
            return Request.WantsModal() ? PartialView("_CategoryEditor", page) : View(page);
        }

        // POST /admin/categories/{id}/delete: deletes a category (refused while it still has active equipment).
        [HttpPost("admin/categories/{id:guid}/delete")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var result = await _categories.DeleteAsync(RequestActor, id, cancellationToken);
            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            TempData[result.Succeeded ? "CategoryFeedback" : "CategoryError"] = result.Message;
            return Redirect("/admin/categories");
        }

        // POST /admin/categories/save: saves the Add/Edit category modal.
        [HttpPost("admin/categories/save")]
        public async Task<IActionResult> Save([Bind(Prefix = "Input")] CategoryEditModel input, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return await InvalidAsync(input, cancellationToken);
            }

            var result = await _categories.SaveAsync(RequestActor, input, cancellationToken);
            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors ?? [])
                {
                    ModelState.AddModelError(string.IsNullOrEmpty(error.Field) ? "" : "Input." + error.Field, error.Message);
                }

                return await InvalidAsync(input, cancellationToken);
            }

            if (Request.IsFetch())
            {
                return ModalForms.Saved(result.Message);
            }

            TempData["CategoryFeedback"] = result.Message;
            return Redirect("/admin/categories");
        }

        // Save failed: send the errors back to the modal (JSON) or re-show the page.
        private async Task<IActionResult> InvalidAsync(CategoryEditModel input, CancellationToken ct)
        {
            return Request.IsFetch() ? ModalForms.Rejected(ModelState) : View("Index", await PageAsync(input, ct));
        }

        // Builds the page model: the list, messages, and the prefix of the category being edited.
        private async Task<CategoriesPageModel> PageAsync(CategoryEditModel input, CancellationToken ct, string? q = null)
        {
            var page = await _categories.GetPageAsync(q, ct);
            // The list holds the category being edited, so its asset code prefix needs no extra query.
            var prefix = input.Id is { } id ? page.Categories.FirstOrDefault(x => x.Id == id)?.AssetCodePrefix : null;
            return new(input, page.Categories, TempData["CategoryFeedback"] as string, page.Query, page.Total, TempData["CategoryError"] as string, prefix);
        }
    }
}
