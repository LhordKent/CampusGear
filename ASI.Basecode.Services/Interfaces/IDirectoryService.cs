using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IDirectoryService
    {
        Task<IReadOnlyList<DirectoryCategory>> GetCategoriesAsync(CancellationToken ct);
        Task<DirectoryBorrowersPage> GetBorrowersAsync(string? query, string? eligibility, int page, CancellationToken ct);
    }
}
