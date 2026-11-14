using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class DevelopmentEmailSenderMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.DevelopmentEmailSenderMessages",
            typeof(DevelopmentEmailSenderMessages).Assembly
        );

        public static string DevelopmentOnly => Get(nameof(DevelopmentOnly));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
