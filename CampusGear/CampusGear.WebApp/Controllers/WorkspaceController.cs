using System.Security.Claims;
using CampusGear.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CampusGear.Controllers;
public sealed class WorkspaceController(IDashboardService dashboards) : Controller
{
    [Authorize(Roles = "Custodian")]
    [HttpGet("custodian/dashboard")]
    public async Task<IActionResult> Custodian(CancellationToken cancellationToken) =>
        View("CustodianDashboard", await dashboards.GetCustodianAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken));
    [Authorize(Roles = "Administrator")]
    [HttpGet("admin/dashboard")]
    public async Task<IActionResult> Administrator(CancellationToken cancellationToken) =>
        View("AdminDashboard", await dashboards.GetAdministratorAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken));
}
