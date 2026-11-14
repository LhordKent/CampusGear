using AutoMapper;
using CampusGear.Resources.Constants;
using CampusGear.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusGear.WebApp.Controllers
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

        [HttpGet("custodian/categories")]
        public async Task<IActionResult> Categories(CancellationToken cancellationToken)
        {
            ViewData["WorkspaceRole"] = RoleNames.Custodian;
            return View(await _directory.GetCategoriesAsync(cancellationToken));
        }

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
