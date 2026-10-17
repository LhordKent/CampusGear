using Microsoft.AspNetCore.Identity;

namespace CampusGear.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    // Computed by SQL Server from FirstName and LastName so queries can search and sort on it.
    public string FullName { get; private set; } = string.Empty;
    public AccountApprovalStatus ApprovalStatus { get; set; } = AccountApprovalStatus.Approved;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public BorrowerProfile? BorrowerProfile { get; set; }
    public ICollection<EmailChallenge> EmailChallenges { get; set; } = new List<EmailChallenge>();
}
