using CampusGear.Models;
namespace CampusGear.Data.QueryModels;
public sealed record DirectoryUnitData(Guid Id, string Name, string AssetCode, EquipmentCondition Condition, string? Location);
public sealed record DirectoryCategoryData(string Name, string? Description, bool IsActive, IReadOnlyList<DirectoryUnitData> Units);
public sealed record DirectoryBorrowerData(string FullName, string Email, string? StudentNumber, string? Department,
    string? ContactNumber, bool IsEligible, string? EligibilityNotes, bool IsActive);
public sealed record DirectoryPageData(IReadOnlyList<DirectoryBorrowerData> Rows, int Page, int Pages, int Total);
