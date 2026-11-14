using System.Globalization;

namespace CampusGear.Services.Helpers
{
    public static class ManilaTime
    {
        private static readonly TimeZoneInfo ManilaZone = ResolveZone();

        public static bool TryParseInput(string? value, out DateTime utc)
        {
            utc = default;
            if (
                !DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
                || local.Year is < 2000 or > 2100
            )
            {
                return false;
            }

            utc = Utc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
            return true;
        }

        public static bool TryParseDate(string? value, out DateOnly date)
        {
            return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                && date.Year is >= 2000 and <= 2100;
        }

        public static TimeZoneInfo ResolveZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            }
        }

        public static DateTime Utc(DateTime local)
        {
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), ManilaZone);
        }

        public static DateTime Local(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ManilaZone);
        }

        public static string Format(DateTime utc)
        {
            return Local(utc).ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture);
        }

        public static string Input(DateTime local) => local.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

        public static string FormatAudit(DateTime utc)
        {
            return Local(utc).ToString("MMM d, yyyy · h:mm:ss tt", CultureInfo.InvariantCulture);
        }
    }
}
