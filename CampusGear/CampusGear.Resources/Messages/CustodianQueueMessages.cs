using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class CustodianQueueMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.CustodianQueueMessages",
            typeof(CustodianQueueMessages).Assembly
        );

        public static string ItemUnavailable => Get(nameof(ItemUnavailable));

        public static string ReleaseNotStarted => Get(nameof(ReleaseNotStarted));

        public static string BookingEnded => Get(nameof(BookingEnded));

        public static string HoldExpired => Get(nameof(HoldExpired));

        public static string InvalidFilterDates => Get(nameof(InvalidFilterDates));

        public static string FilterDatesOutOfRange => Get(nameof(FilterDatesOutOfRange));

        public static string InvalidDateRange => Get(nameof(InvalidDateRange));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
