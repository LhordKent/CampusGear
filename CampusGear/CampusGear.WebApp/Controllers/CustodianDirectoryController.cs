using CampusGear.Data;
using CampusGear.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Controllers;

/// <summary>
/// Read-only reference data for the custodian: equipment categories with each unit's status, and borrower profiles
/// with their eligibility. There are deliberately no POST actions; creating and editing these records is the
/// administrator's job (AdminCategoriesController, AdminInventoryController, AdminUsersController).
/// </summary>
[Authorize(Roles = "Custodian")]
public sealed class CustodianDirectoryController(ApplicationDbContext db, TimeProvider clock) : Controller
{
    private const int PageSize = 15;

    [HttpGet("custodian/categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken)
    {
        var statuses = await ItemStatuses.ForAllAsync(db, clock.GetUtcNow().UtcDateTime, cancellationToken);
        var categories = await db.EquipmentCategories.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id, x.Name, x.Description, x.IsActive,
                Units = x.Items.OrderByDescending(i => i.CreatedAtUtc).ThenBy(i => i.AssetCode)
                    .Select(i => new { i.Id, i.Name, i.AssetCode, i.Condition, i.Location }).ToList()
            })
            .ToListAsync(cancellationToken);
        ViewData["WorkspaceRole"] = "Custodian";
        return View(categories.Select(x => new DirectoryCategory(x.Name, x.Description, x.IsActive,
            x.Units.Select(u => new DirectoryUnit(u.Name, u.AssetCode, u.Condition, u.Location,
                statuses.GetValueOrDefault(u.Id, ItemStatus.Inactive))).ToList())).ToList());
    }

    [HttpGet("custodian/borrowers")]
    public async Task<IActionResult> Borrowers(string? q, string? eligibility, int page = 1,
        CancellationToken cancellationToken = default)
    {
        q = q?.Trim() ?? string.Empty;
        if (q.Length > 120) q = q[..120];
        eligibility = eligibility is "eligible" or "paused" ? eligibility : "all";
        // Only accounts in the Borrower role; staff accounts never appear here.
        var query = db.BorrowerProfiles.AsNoTracking().Where(x =>
            db.UserRoles.Any(ur => ur.UserId == x.UserId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Borrower")));
        if (q.Length > 0)
            query = query.Where(x => x.User.FullName.Contains(q) || x.User.Email!.Contains(q) ||
                x.StudentNumber != null && x.StudentNumber.Contains(q) || x.Department != null && x.Department.Contains(q));
        if (eligibility == "eligible") query = query.Where(x => x.IsEligible);
        if (eligibility == "paused") query = query.Where(x => !x.IsEligible);

        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        var rows = await query.OrderByDescending(x => x.User.CreatedAtUtc).ThenBy(x => x.User.FullName)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(x => new DirectoryBorrower(x.User.FullName, x.User.Email ?? string.Empty, x.StudentNumber,
                x.Department, x.ContactNumber, x.IsEligible, x.EligibilityNotes, x.User.IsActive))
            .ToListAsync(cancellationToken);
        ViewData["WorkspaceRole"] = "Custodian";
        return View(new DirectoryBorrowersPage(rows, q, eligibility, page, pages, total));
    }
}

public sealed record DirectoryUnit(string Name, string AssetCode, EquipmentCondition Condition, string? Location, ItemStatus Status);
public sealed record DirectoryCategory(string Name, string? Description, bool IsActive, IReadOnlyList<DirectoryUnit> Units);
public sealed record DirectoryBorrower(string FullName, string Email, string? StudentNumber, string? Department,
    string? ContactNumber, bool IsEligible, string? EligibilityNotes, bool IsActive);
public sealed record DirectoryBorrowersPage(IReadOnlyList<DirectoryBorrower> Rows, string Query, string Eligibility,
    int Page, int PageCount, int Total);
