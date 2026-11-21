using System.Diagnostics;
using System.Security.Claims;
using AutoMapper;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Authentication;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// =====================================================================
// HOME CONTROLLER
// The site root (/), the custodian and admin dashboards, and the error pages.
// FLOW: dashboard URL -> this controller -> DashboardService -> DashboardReadRepository -> database.
// =====================================================================
namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// The site entry point, the staff dashboards, and the error/status pages.
    /// </summary>
    public sealed class HomeController : Mvc.ControllerBase<HomeController>
    {
        private readonly IDashboardService _dashboards;

        public HomeController(
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

        /// <summary>The site root sends everyone to sign in; a signed-in user is forwarded from there to their workspace.</summary>
        [AllowAnonymous]
        [HttpGet("/")]
        public IActionResult Index()
        {
            return Redirect("/auth/login");
        }

        // WBS 45.00 | Each staff dashboard route selects a model for the matching workspace and role.
        [Authorize(Roles = RoleNames.Custodian)]
        [HttpGet("custodian/dashboard")]
        public async Task<IActionResult> Custodian(CancellationToken cancellationToken)
        {
            return View(
                "Index",
                await _dashboards.GetCustodianAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken)
            );
        }

        // GET /admin/dashboard: the administrator's home page (counts, charts, recent activity).
        [Authorize(Roles = RoleNames.Administrator)]
        [HttpGet("admin/dashboard")]
        public async Task<IActionResult> Administrator(CancellationToken cancellationToken)
        {
            return View(
                "Index",
                await _dashboards.GetAdministratorAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken)
            );
        }

        /// <summary>
        /// Renders the body for error status codes that reach the status-code pages middleware (Startup.cs). The status code
        /// itself is never changed: a 403 stays 403. A browser navigating to a forbidden page gets the access-denied page with
        /// that 403; background requests, form posts and every other code get the bare status.
        /// </summary>
        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Route("status/{code:int}")]
        public IActionResult Status(int code)
        {
            if (code == StatusCodes.Status403Forbidden && AuthResponses.IsPageNavigation(Request))
            {
                return new ViewResult { ViewName = "~/Views/Account/AccessDenied.cshtml", StatusCode = code };
            }

            return StatusCode(code);
        }

        /// <summary>Unhandled-exception page used outside Development (UseExceptionHandler in Startup.cs).</summary>
        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Route("Error")]
        [IgnoreAntiforgeryToken]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var model = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };
            return View("~/Views/Shared/Error.cshtml", model);
        }
    }
}
