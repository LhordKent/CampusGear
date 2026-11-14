using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class CategoryMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.CategoryMessages",
            typeof(CategoryMessages).Assembly
        );

        public static string NameRequired => Get(nameof(NameRequired));

        public static string StaleCategory => Get(nameof(StaleCategory));

        public static string ActiveEquipmentPreventsDeactivation => Get(nameof(ActiveEquipmentPreventsDeactivation));

        public static string CategorySaved => Get(nameof(CategorySaved));

        public static string RecordBusy => Get(nameof(RecordBusy));

        public static string DuplicateOrStaleCategory => Get(nameof(DuplicateOrStaleCategory));

        public static string InvalidNameLength => Get(nameof(InvalidNameLength));

        public static string CategoryAdded => Get(nameof(CategoryAdded));

        public static string DuplicateCategory => Get(nameof(DuplicateCategory));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
