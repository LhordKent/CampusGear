using CampusGear.Models;
namespace CampusGear.Services.ServiceModels;
public sealed class UserEditCommand
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = "Borrower";
    public bool IsActive { get; set; }
    public string? StudentNumber { get; set; }
    public string? Department { get; set; }
    public bool IsEligible { get; set; } = true;
    public string? EligibilityNotes { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
    public string? ProfileRowVersion { get; set; }
}
public sealed class UserCreateCommand
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Borrower";
    public string Password { get; set; } = string.Empty;
    public string? StudentNumber { get; set; }
    public string? Department { get; set; }
    public string? ContactNumber { get; set; }
}
public sealed record UserEditPage(string Id, string Email, AccountApprovalStatus ApprovalStatus, string? ContactNumber, string CurrentRole, bool HasBorrowerProfile, bool IsSelf, UserEditCommand Input);
