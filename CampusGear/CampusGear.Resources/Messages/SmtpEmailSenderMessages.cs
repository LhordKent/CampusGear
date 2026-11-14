using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class SmtpEmailSenderMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.SmtpEmailSenderMessages",
            typeof(SmtpEmailSenderMessages).Assembly
        );

        public static string ConfigurationRequired => Get(nameof(ConfigurationRequired));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
