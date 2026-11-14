using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class EquipmentImageStoreMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.EquipmentImageStoreMessages",
            typeof(EquipmentImageStoreMessages).Assembly
        );

        public static string ImageRequired => Get(nameof(ImageRequired));

        public static string ImageTooLarge => Get(nameof(ImageTooLarge));

        public static string UnsupportedImageType => Get(nameof(UnsupportedImageType));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
