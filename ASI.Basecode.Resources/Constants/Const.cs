namespace ASI.Basecode.Resources.Constants
{
    public static class RoleNames
    {
        public const string Administrator = "Administrator";
        public const string Custodian = "Custodian";
        public const string Borrower = "Borrower";
        public const string CustodianOrAdministrator = "Custodian,Administrator";
        public const string AdministratorOrCustodian = "Administrator,Custodian";
    }

    public static class BorrowingLimits
    {
        public const int MaxUnitsPerRequest = 10;
    }

    /// <summary>
    /// Departments offered by the sign-up and admin account forms. Edit this list to match the campus;
    /// names already saved on accounts are kept even if they are later removed here.
    /// </summary>
    public static class Departments
    {
        public static readonly IReadOnlyList<string> All = new[]
        {
            "College of Computer Studies",
            "College of Engineering",
            "College of Business and Accountancy",
            "College of Teacher Education",
            "College of Arts and Sciences",
            "College of Criminal Justice",
            "College of Nursing",
            "College of Hospitality Management",
            "College of Maritime Education",
            "College of Customs Administration",
            "Senior High School",
        };

        /// <summary>The listed spelling of <paramref name="value"/> (case-insensitive match), or null if it is not listed.</summary>
        public static string? Find(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : All.FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        }
    }
}
