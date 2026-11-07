using CampusGear.Data.QueryModels;
namespace CampusGear.Data.Interfaces;
public interface IDirectoryReadRepository
{
    Task<IReadOnlyList<DirectoryCategoryData>> CategoriesAsync(CancellationToken ct);
    Task<DirectoryPageData> BorrowersAsync(string query, string eligibility, int page, CancellationToken ct);
}
