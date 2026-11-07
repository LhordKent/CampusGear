using System.ComponentModel.DataAnnotations;
using CampusGear.Models;
namespace CampusGear.Controllers;
public sealed class AdminUserEditInput
{
    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;
    [Required]
    public string Role { get; set; } = "Borrower";
    public bool IsActive { get; set; }
    [StringLength(64)]
    public string? StudentNumber { get; set; }
    [StringLength(160)]
    public string? Department { get; set; }
    public bool IsEligible { get; set; } = true;
    [StringLength(1000)]
    public string? EligibilityNotes { get; set; }
    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
    public string? ProfileRowVersion { get; set; }
}
public sealed class AdminUserCreateInput
{
    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string Role { get; set; } = "Borrower";
    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "Temporary password")]
    public string Password { get; set; } = string.Empty;
    [StringLength(64), Display(Name = "Student ID Number")]
    public string? StudentNumber { get; set; }
    [StringLength(160)]
    public string? Department { get; set; }
    [StringLength(32), Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }
}
public sealed record AdminUserEditPage(string Id, string Email, AccountApprovalStatus ApprovalStatus,
    string? ContactNumber, string CurrentRole, bool HasBorrowerProfile, bool IsSelf, AdminUserEditInput Input);
