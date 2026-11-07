using CampusGear.Controllers;
using CampusGear.Data.Interfaces;
using CampusGear.Models;
namespace CampusGear.Services;
public sealed class DirectoryService(IDirectoryReadRepository reads, IEquipmentAvailabilityService availability, TimeProvider clock) : IDirectoryService
{
    public async Task<IReadOnlyList<DirectoryCategory>> GetCategoriesAsync(CancellationToken ct)
    {
        var statuses = await availability.GetStatesAsync(clock.GetUtcNow().UtcDateTime, ct);
        return (await reads.CategoriesAsync(ct)).Select(x => new DirectoryCategory(x.Name, x.Description, x.IsActive,
            x.Units.Select(u => new DirectoryUnit(u.Name, u.AssetCode, u.Condition, u.Location, statuses.GetValueOrDefault(u.Id, ItemStatus.Inactive))).ToList())).ToList();
    }
    public async Task<DirectoryBorrowersPage> GetBorrowersAsync(string? query, string? eligibility, int page, CancellationToken ct)
    {
        query = query?.Trim() ?? ""; if (query.Length > 120) query = query[..120];
        eligibility = eligibility is "eligible" or "paused" ? eligibility : "all";
        var data = await reads.BorrowersAsync(query, eligibility, page, ct);
        return new(data.Rows.Select(x => new DirectoryBorrower(x.FullName, x.Email, x.StudentNumber, x.Department,
            x.ContactNumber, x.IsEligible, x.EligibilityNotes, x.IsActive)).ToList(), query, eligibility, data.Page, data.Pages, data.Total);
    }
}
