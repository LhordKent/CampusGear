using System.ComponentModel.DataAnnotations;
namespace CampusGear.Controllers;
public sealed class LoginInput
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class SignUpInput
{
    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, RegularExpression(@"^[0-9]+$", ErrorMessage = "Student ID Number must contain numbers only."), StringLength(64), Display(Name = "Student ID Number")]
    public string StudentIdNumber { get; set; } = string.Empty;

    [Required, EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^[0-9]*$", ErrorMessage = "Mobile number must contain numbers only."),
     StringLength(11, MinimumLength = 11, ErrorMessage = "Mobile number must be exactly 11 digits (e.g. 09XXXXXXXXX)."), Display(Name = "Mobile number")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class CodeInput
{
    [Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "Enter the six-digit code.")]
    public string Code { get; set; } = string.Empty;
}

public sealed class EmailInput
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public sealed class NewPasswordInput
{
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
