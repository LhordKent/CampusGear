using CampusGear.Data.QueryModels;
using CampusGear.Models;
namespace CampusGear.Data.Interfaces;
public interface IAccountRepository
{
    Task<bool> HasBorrowerRoleAsync(string userId, CancellationToken ct);
    Task<AccountPageData> ListAsync(string search, string role, string status, int page, CancellationToken ct);
    Task<ApplicationUser?> FindAsync(string id, CancellationToken ct);
    Task<ApplicationUser?> FindFreshAsync(string id, CancellationToken ct);
    Task<ApplicationUser?> FindWithProfileAsync(string id, CancellationToken ct);
    Task<ApplicationUser?> FindWithProfileFreshAsync(string id, CancellationToken ct);
    Task<bool> StudentExistsAsync(string number, string? excludingUser, CancellationToken ct);
    Task<bool> HasOtherAdministratorAsync(string excludingUser, CancellationToken ct);
    Task<bool> HasHistoryAsync(string userId, CancellationToken ct);
    Task PrepareDeletionAsync(ApplicationUser user, CancellationToken ct);
    Task ReloadProfileAsync(BorrowerProfile profile, CancellationToken ct);
    Task<bool> ReloadUserAsync(ApplicationUser user, CancellationToken ct);
    void AddProfile(BorrowerProfile profile);
}
