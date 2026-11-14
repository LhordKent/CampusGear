using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class EmailChallengeMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.EmailChallengeMessages",
            typeof(EmailChallengeMessages).Assembly
        );

        public static string UnrequestedCodeNotice => Get(nameof(UnrequestedCodeNotice));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
