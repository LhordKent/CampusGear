using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IUserRepository
    {
        Task<bool> HasBorrowerRoleAsync(string userId, CancellationToken ct);
        Task<AccountPageData> ListAsync(string search, string role, string status, int page, CancellationToken ct);
        Task<User?> FindAsync(string id, CancellationToken ct);
        Task<User?> FindFreshAsync(string id, CancellationToken ct);
        Task<User?> FindWithProfileAsync(string id, CancellationToken ct);
        Task<User?> FindWithProfileFreshAsync(string id, CancellationToken ct);
        Task<bool> StudentExistsAsync(string number, string? excludingUser, CancellationToken ct);
        Task<bool> HasOtherAdministratorAsync(string excludingUser, CancellationToken ct);
        Task<bool> HasHistoryAsync(string userId, CancellationToken ct);
        Task PrepareDeletionAsync(User user, CancellationToken ct);
        Task ReloadProfileAsync(BorrowerProfile profile, CancellationToken ct);
        Task<bool> ReloadUserAsync(User user, CancellationToken ct);
        void AddProfile(BorrowerProfile profile);
    }
}
