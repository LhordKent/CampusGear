using System.Security.Claims;
using CampusGear.Services;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CampusGear.Controllers;
[Authorize(Roles = "Administrator,Custodian")]
[AutoValidateAntiforgeryToken]
public sealed class MaintenanceController(IMaintenanceService maintenance) : Controller
{
    private RequestContext Actor() => new(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    [HttpGet("admin/maintenance")]
    public async Task<IActionResult> Index(string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        var result = await maintenance.GetPageAsync(status, q, page, cancellationToken);
        var data = result.Data;
        ViewData["WorkspaceRole"] = User.IsInRole("Administrator") ? "Administrator" : "Custodian";
        return View(new MaintenancePageModel(data.Cases, data.Choices.Select(x => new MaintenanceItemOption(x.Id, x.Name, x.AssetCode, x.Condition,
            x.HasCase ? "already in maintenance" : x.OnLoan ? "on loan" : null)).ToList(),
            result.Status, result.Query, data.Page, data.Pages, data.Total, data.OpenTotal, data.ClosedTotal,
            TempData["MaintenanceFeedback"] as string, TempData["MaintenanceTone"] as string ?? "success"));
    }
    [HttpPost("admin/maintenance/open")]
    public async Task<IActionResult> Open(Guid equipmentItemId, string? description, CancellationToken cancellationToken) =>
        Result(await maintenance.OpenAsync(Actor(), equipmentItemId, description, cancellationToken));
    [HttpPost("admin/maintenance/{id:guid}/update")]
    public async Task<IActionResult> Update(Guid id, MaintenanceUpdateInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Result(OperationResult<bool>.Invalid("Choose a valid status and equipment condition, and keep the notes to 2000 characters."));
        return Result(await maintenance.UpdateAsync(Actor(), id, new(input.RowVersion, input.Status, input.Condition, input.Resolution), cancellationToken));
    }
    private IActionResult Result(OperationResult<bool> result)
    {
        var ok = result.Succeeded;
        var field = result.Errors?.FirstOrDefault()?.Field;
        if (field == "") field = null;
        if (Request.Headers.XRequestedWith == "fetch")
            return new JsonResult(new { ok, message = result.Message, field }) { StatusCode = result.Outcome == OperationOutcome.NotFound ? 404 : ok ? 200 : 422 };
        TempData["MaintenanceFeedback"] = result.Message;
        TempData["MaintenanceTone"] = ok ? "success" : "error";
        return Redirect("/admin/maintenance");
    }
}
