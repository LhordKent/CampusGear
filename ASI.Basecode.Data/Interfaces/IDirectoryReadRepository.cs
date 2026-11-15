using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IDirectoryReadRepository
    {
        Task<IReadOnlyList<DirectoryCategoryData>> CategoriesAsync(CancellationToken ct);
        Task<DirectoryPageData> BorrowersAsync(string query, string eligibility, int page, CancellationToken ct);
    }
}
