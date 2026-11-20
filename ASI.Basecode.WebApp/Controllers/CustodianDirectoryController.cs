using AutoMapper;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// =====================================================================
// CUSTODIAN DIRECTORY CONTROLLER  (custodians only, read-only)
// Lookup pages: equipment by category, and the list of student borrowers.
// FLOW: page -> this controller -> DirectoryService -> DirectoryReadRepository -> database.
// =====================================================================
namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = RoleNames.Custodian)]
    public sealed class CustodianDirectoryController : Mvc.ControllerBase<CustodianDirectoryController>
    {
        private readonly IDirectoryService _directory;

        public CustodianDirectoryController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IDirectoryService directory
        )
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _directory = directory;
        }

        // GET /custodian/categories: every category with its units (code, condition, location).
        [HttpGet("custodian/categories")]
        public async Task<IActionResult> Categories(CancellationToken cancellationToken)
        {
            ViewData["WorkspaceRole"] = RoleNames.Custodian;
            return View(await _directory.GetCategoriesAsync(cancellationToken));
        }

        // GET /custodian/borrowers: the student list with status (active, inactive, borrowing paused).
        [HttpGet("custodian/borrowers")]
        public async Task<IActionResult> Borrowers(
            string? q,
            string? eligibility,
            int page = 1,
            CancellationToken cancellationToken = default
        )
        {
            ViewData["WorkspaceRole"] = RoleNames.Custodian;
            return View(await _directory.GetBorrowersAsync(q, eligibility, page, cancellationToken));
        }
    }
}
