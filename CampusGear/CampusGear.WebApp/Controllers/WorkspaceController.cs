using System.Security.Claims;
using AutoMapper;
using CampusGear.Resources.Constants;
using CampusGear.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusGear.WebApp.Controllers
{
    public sealed class WorkspaceController : Mvc.ControllerBase<WorkspaceController>
    {
        private readonly IDashboardService _dashboards;

        public WorkspaceController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IDashboardService dashboards
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _dashboards = dashboards;
        }

        [Authorize(Roles = RoleNames.Custodian)]
        [HttpGet("custodian/dashboard")]
        public async Task<IActionResult> Custodian(CancellationToken cancellationToken)
        {
            return View(
                "CustodianDashboard",
                await _dashboards.GetCustodianAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken)
            );
        }

        [Authorize(Roles = RoleNames.Administrator)]
        [HttpGet("admin/dashboard")]
        public async Task<IActionResult> Administrator(CancellationToken cancellationToken)
        {
            return View(
                "AdminDashboard",
                await _dashboards.GetAdministratorAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken)
            );
        }
    }
}
