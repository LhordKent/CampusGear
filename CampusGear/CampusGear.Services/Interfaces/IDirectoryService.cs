using CampusGear.Controllers;
namespace CampusGear.Services;
public interface IDirectoryService
{
    Task<IReadOnlyList<DirectoryCategory>> GetCategoriesAsync(CancellationToken ct);
    Task<DirectoryBorrowersPage> GetBorrowersAsync(string? query, string? eligibility, int page, CancellationToken ct);
}
