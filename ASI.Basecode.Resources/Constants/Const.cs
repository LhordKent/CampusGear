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

    /// <summary>
    /// Fair-use limits on borrower requests, enforced by ReservationService when a request is created (under the
    /// borrower lock, so they cannot be raced) and mirrored by the booking picker so it never offers a time the
    /// server would refuse.
    /// </summary>
    public static class BorrowingLimits
    {
        public const int MaxUnitsPerRequest = 10;

        /// <summary>
        /// Most bookings one borrower can hold at once: pending (hold still live), approved (not yet ended) or
        /// borrowed (not yet returned). Units requested together for the same time count as one booking.
        /// </summary>
        public const int MaxActiveBookings = 3;

        /// <summary>Longest single booking, from borrowing time to expected return.</summary>
        public const int MaxBookingDays = 3;

        /// <summary>How far ahead a booking may start.</summary>
        public const int BookingWindowDays = 14;

        public static readonly TimeSpan MaxBookingDuration = TimeSpan.FromDays(MaxBookingDays);
        public static readonly TimeSpan BookingWindow = TimeSpan.FromDays(BookingWindowDays);
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
