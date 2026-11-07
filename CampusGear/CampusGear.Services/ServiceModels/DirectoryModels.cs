using CampusGear.Models;
namespace CampusGear.Controllers;
public sealed record DirectoryUnit(string Name, string AssetCode, EquipmentCondition Condition, string? Location, ItemStatus Status);
public sealed record DirectoryCategory(string Name, string? Description, bool IsActive, IReadOnlyList<DirectoryUnit> Units);
public sealed record DirectoryBorrower(string FullName, string Email, string? StudentNumber, string? Department,
    string? ContactNumber, bool IsEligible, string? EligibilityNotes, bool IsActive);
public sealed record DirectoryBorrowersPage(IReadOnlyList<DirectoryBorrower> Rows, string Query, string Eligibility,
    int Page, int PageCount, int Total);
