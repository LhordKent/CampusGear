using System.ComponentModel.DataAnnotations;
using CampusGear.Controllers;

namespace CampusGear.IntegrationTests;

/// <summary>Sign-up field rules that need no database: numbers-only Student ID and 11-digit mobile number.</summary>
public sealed class SignUpInputValidationTests
{
    private static SignUpInput Valid() => new()
    {
        FirstName = "Maria", LastName = "Dela Cruz", StudentIdNumber = "25237611",
        Email = "maria@integration.test", MobileNumber = "09123456789",
        Password = "Str0ng#Passw0rd", ConfirmPassword = "Str0ng#Passw0rd"
    };

    private static List<ValidationResult> Validate(SignUpInput input)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);
        return results;
    }

    private static string[] Errors(List<ValidationResult> results, string member) =>
        results.Where(x => x.MemberNames.Contains(member)).Select(x => x.ErrorMessage!).ToArray();

    [Fact]
    public void Valid_input_has_no_errors() => Assert.Empty(Validate(Valid()));

    [Theory]
    [InlineData("0912345")]        // too short
    [InlineData("0912345678")]     // 10 digits
    [InlineData("091234567890")]   // 12 digits
    [InlineData("09abcdefghi")]    // letters
    [InlineData("0912 345 6789")]  // spaces
    public void Mobile_number_must_be_exactly_11_digits(string mobile)
    {
        var input = Valid();
        input.MobileNumber = mobile;
        Assert.NotEmpty(Errors(Validate(input), nameof(SignUpInput.MobileNumber)));
    }

    [Fact]
    public void Mobile_number_with_letters_says_numbers_only()
    {
        var input = Valid();
        input.MobileNumber = "09abcdefghi";
        Assert.Contains("Mobile number must contain numbers only.",
            Errors(Validate(input), nameof(SignUpInput.MobileNumber)));
    }

    [Theory]
    [InlineData("SID-1234")]
    [InlineData("2026-0001")]
    [InlineData("2523761a")]
    [InlineData("25 237 611")]
    public void Student_id_must_be_numbers_only(string studentId)
    {
        var input = Valid();
        input.StudentIdNumber = studentId;
        Assert.Contains("Student ID Number must contain numbers only.",
            Errors(Validate(input), nameof(SignUpInput.StudentIdNumber)));
    }

    [Theory]
    [InlineData("7")]
    [InlineData("25237611")]
    [InlineData("12345678901")]
    public void Student_id_accepts_digits(string studentId)
    {
        var input = Valid();
        input.StudentIdNumber = studentId;
        Assert.Empty(Errors(Validate(input), nameof(SignUpInput.StudentIdNumber)));
    }
}
