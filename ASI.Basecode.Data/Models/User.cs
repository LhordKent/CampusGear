using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Identity;

// USER (table: AspNetUsers)  -  a sign-in account (ASP.NET Identity). Roles: Administrator, Custodian, Borrower.
namespace ASI.Basecode.Data.Models
{
    public sealed class User : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        // Computed by SQL Server from FirstName and LastName so queries can search and sort on it.
        public string FullName { get; private set; } = string.Empty;

        // Students who sign up start as PendingApproval until an admin approves them.
        public AccountApprovalStatus ApprovalStatus { get; set; } = AccountApprovalStatus.Approved;

        // false = deactivated: can't sign in at all.
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public BorrowerProfile? BorrowerProfile { get; set; }

        public ICollection<EmailChallenge> EmailChallenges { get; set; } = new List<EmailChallenge>();
    }
}
