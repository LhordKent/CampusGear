using Microsoft.AspNetCore.Identity;

namespace CampusGear.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public BorrowerProfile? BorrowerProfile { get; set; }
    public ICollection<EmailChallenge> EmailChallenges { get; set; } = new List<EmailChallenge>();
}
