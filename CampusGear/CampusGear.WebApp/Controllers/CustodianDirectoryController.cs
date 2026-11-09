using CampusGear.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CampusGear.Controllers;
[Authorize(Roles = "Custodian")]
public sealed class CustodianDirectoryController(IDirectoryService directory) : Controller
{
    [HttpGet("custodian/categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        ViewData["WorkspaceRole"] = "Custodian";
        return View(await directory.GetCategoriesAsync(cancellationToken));
    }
    [HttpGet("custodian/borrowers")]
    public async Task<IActionResult> Borrowers(string? q, string? eligibility, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["WorkspaceRole"] = "Custodian";
        return View(await directory.GetBorrowersAsync(q, eligibility, page, cancellationToken));
    }
}
