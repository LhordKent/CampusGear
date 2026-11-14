using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class BorrowingHistoryMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.BorrowingHistoryMessages",
            typeof(BorrowingHistoryMessages).Assembly
        );

        public static string InvalidFromDate => Get(nameof(InvalidFromDate));

        public static string InvalidToDate => Get(nameof(InvalidToDate));

        public static string InvalidDateRange => Get(nameof(InvalidDateRange));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
