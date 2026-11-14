using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class EquipmentCatalogMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.EquipmentCatalogMessages",
            typeof(EquipmentCatalogMessages).Assembly
        );

        public static string InvalidBookingDates => Get(nameof(InvalidBookingDates));

        public static string InvalidDateRange => Get(nameof(InvalidDateRange));

        public static string FutureStartRequired => Get(nameof(FutureStartRequired));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
