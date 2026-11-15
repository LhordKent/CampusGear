using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.ServiceModels
{
    public sealed record AccountChallengeState(string UserId, string SecurityStamp);

    public sealed record AdminAuditPage(IReadOnlyList<AdminAuditRow> Rows, string Query, int Page, int PageCount, int Total);

    public sealed record AdminAuditRow(
        Guid Id,
        string TimeManila,
        string Action,
        string ActorName,
        string? ActorEmail,
        string EntityType,
        string EntityId,
        string Outcome,
        string? IpAddress,
        string? CorrelationId,
        string? BeforeJson,
        string? AfterJson
    );

    public sealed record AdminUserRow(
        string Id,
        string FullName,
        string Email,
        string Role,
        bool IsActive,
        AccountApprovalStatus ApprovalStatus,
        string? StudentNumber,
        string? Department,
        bool? IsEligible,
        string RegisteredManila
    );

    public sealed record AdminUsersPage(
        IReadOnlyList<AdminUserRow> Rows,
        string Query,
        string Role,
        string Status,
        int Page,
        int PageCount,
        int Total,
        int PendingTotal,
        string? Feedback,
        string? Error
    );

    public sealed record AuthenticationResult(AuthenticationStatus Status, string? Role = null, string? Notice = null);

    public enum AuthenticationStatus
    {
        CredentialsRequired,
        VerificationRequired,
        SignedIn,
    }

    public sealed record ChallengeIssueResult(ChallengeIssueStatus Status, TimeSpan? RetryAfter = null);

    public enum ChallengeIssueStatus
    {
        Sent,
        Cooldown,
        DeliveryUnavailable,
    }

    public enum ChallengeVerifyStatus
    {
        Verified,
        Invalid,
        Expired,
        TooManyAttempts,
    }

    public sealed class CodeInput
    {
        [Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "Enter the six-digit code.")]
        public string Code { get; set; } = string.Empty;
    }

    public sealed record CurrentAccountDisplay(string Name, string Initial);

    public sealed class EmailInput
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;
    }

    public sealed class LoginInput
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public sealed class NewPasswordInput
    {
        [Required, StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [Required, Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public sealed record PasswordResetAuthorization(string UserId, string SecurityStamp, long ExpiresAtUnixSeconds);

    public sealed record PasswordResetResult(PasswordResetStatus Status, string? Notice = null);

    public enum PasswordResetStatus
    {
        RequestRequired,
        CodeRequired,
        PasswordRequired,
        Completed,
    }

    public sealed record RegistrationResult(RegistrationStatus Status);

    public enum RegistrationStatus
    {
        InputRequired,
        Submitted,
    }

    public sealed class SignUpInput
    {
        [Required, StringLength(100), Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100), Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "Student ID Number must contain numbers only.")]
        [StringLength(64)]
        [Display(Name = "Student ID Number")]
        public string StudentIdNumber { get; set; } = string.Empty;

        [Required, EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[0-9]*$", ErrorMessage = "Mobile number must contain numbers only.")]
        [StringLength(11, MinimumLength = 11, ErrorMessage = "Mobile number must be exactly 11 digits (e.g. 09XXXXXXXXX).")]
        [Display(Name = "Mobile number")]
        public string MobileNumber { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public sealed class UserCreateModel
    {
        [Required, StringLength(100), Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100), Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = RoleNames.Borrower;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Temporary password")]
        public string Password { get; set; } = string.Empty;

        [StringLength(64), Display(Name = "Student ID Number")]
        public string? StudentNumber { get; set; }

        [StringLength(160)]
        public string? Department { get; set; }

        [StringLength(32), Display(Name = "Contact number")]
        public string? ContactNumber { get; set; }
    }

    public sealed class UserEditModel
    {
        [Required, StringLength(100), Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100), Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = RoleNames.Borrower;

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

    public sealed record UserEditPage(
        string Id,
        string Email,
        AccountApprovalStatus ApprovalStatus,
        string? ContactNumber,
        string CurrentRole,
        bool HasBorrowerProfile,
        bool IsSelf,
        UserEditModel Input,
        string RegisteredManila
    );
}
