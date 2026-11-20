using AutoMapper;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// =====================================================================
// MAINTENANCE CONTROLLER  (administrators and custodians)
// Repair cases for damaged equipment: list them, open a case, update/close a case.
// While a case is open, the item is on "maintenance hold" and can't be booked.
// FLOW: page -> this controller -> MaintenanceService -> MaintenanceRepository -> database.
// =====================================================================
namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = RoleNames.AdministratorOrCustodian)]
    [AutoValidateAntiforgeryToken]
    public sealed class MaintenanceController : Mvc.ControllerBase<MaintenanceController>
    {
        private readonly IMaintenanceService _maintenance;

        public MaintenanceController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IMaintenanceService maintenance
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _maintenance = maintenance;
        }

        // GET /admin/maintenance: the list of repair cases, with filters.
        [HttpGet("admin/maintenance")]
        public async Task<IActionResult> Index(string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
        {
            var result = await _maintenance.GetPageAsync(status, q, page, cancellationToken);
            var data = result.Data;
            ViewData["WorkspaceRole"] = User.IsInRole(RoleNames.Administrator) ? RoleNames.Administrator : RoleNames.Custodian;
            return View(
                new MaintenancePageModel(
                    data.Cases,
                    data.Choices.Select(x => new MaintenanceItemOption(
                            x.Id,
                            x.Name,
                            x.AssetCode,
                            x.Condition,
                            x.HasCase ? "Already in maintenance"
                                : x.OnLoan ? "On loan"
                                : null,
                            x.Category,
                            x.Location,
                            x.ImagePath
                        ))
                        .ToList(),
                    result.Status,
                    result.Query,
                    data.Page,
                    data.Pages,
                    data.Total,
                    data.OpenTotal,
                    data.ClosedTotal,
                    TempData["MaintenanceFeedback"] as string,
                    TempData["MaintenanceTone"] as string ?? "success"
                )
            );
        }

        // POST /admin/maintenance/open: opens a repair case for an item (puts it on hold).
        [HttpPost("admin/maintenance/open")]
        public async Task<IActionResult> Open(Guid equipmentItemId, string? description, CancellationToken cancellationToken)
        {
            return Result(await _maintenance.OpenAsync(RequestActor, equipmentItemId, description, cancellationToken));
        }

        // POST /admin/maintenance/{id}/update: updates a case; closing it can release the hold.
        [HttpPost("admin/maintenance/{id:guid}/update")]
        public async Task<IActionResult> Update(Guid id, MaintenanceEditModel input, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return Result(
                    OperationResult<bool>.Invalid("Choose a valid status and equipment condition, and keep the notes to 2000 characters.")
                );
            }

            return Result(await _maintenance.UpdateAsync(RequestActor, id, input, cancellationToken));
        }

        // Turns the service result into a redirect with a message, or a 404 / access denied.
        private IActionResult Result(OperationResult<bool> result)
        {
            var ok = result.Succeeded;
            var field = result.Errors?.FirstOrDefault()?.Field;
            if (field == "")
            {
                field = null;
            }

            if (Request.Headers.XRequestedWith == "fetch")
            {
                return new JsonResult(
                    new
                    {
                        ok,
                        message = result.Message,
                        field,
                    }
                )
                {
                    StatusCode =
                        result.Outcome == OperationOutcome.NotFound ? 404
                        : ok ? 200
                        : 422,
                };
            }

            TempData["MaintenanceFeedback"] = result.Message;
            TempData["MaintenanceTone"] = ok ? "success" : "error";
            return Redirect("/admin/maintenance");
        }
    }
}
