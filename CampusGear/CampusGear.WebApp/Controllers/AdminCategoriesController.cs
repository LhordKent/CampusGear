using AutoMapper;
using CampusGear.Resources.Constants;
using CampusGear.Services.Interfaces;
using CampusGear.Services.ServiceModels;
using CampusGear.WebApp.Infrastructure;
using CampusGear.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusGear.WebApp.Controllers
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

        [HttpPost("admin/categories/{id:guid}/delete")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var result = await _categories.DeleteAsync(RequestActor, id, cancellationToken);
            if (result.Outcome == OperationOutcome.NotFound)
            {
                return NotFound();
            }

            TempData["CategoryFeedback"] = result.Message;
            return Redirect("/admin/categories");
        }

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

        private async Task<IActionResult> InvalidAsync(CategoryEditModel input, CancellationToken ct)
        {
            return Request.IsFetch() ? ModalForms.Rejected(ModelState) : View("Index", await PageAsync(input, ct));
        }

        private async Task<CategoriesPageModel> PageAsync(CategoryEditModel input, CancellationToken ct, string? q = null)
        {
            var page = await _categories.GetPageAsync(q, ct);
            return new(input, page.Categories, TempData["CategoryFeedback"] as string, page.Query, page.Total);
        }
    }
}
