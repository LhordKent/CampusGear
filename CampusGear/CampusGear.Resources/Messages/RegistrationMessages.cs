using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class RegistrationMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.RegistrationMessages",
            typeof(RegistrationMessages).Assembly
        );

        public static string EmailAlreadyRegistered => Get(nameof(EmailAlreadyRegistered));

        public static string StudentNumberAlreadyRegistered => Get(nameof(StudentNumberAlreadyRegistered));

        public static string AccountCreationUnavailable => Get(nameof(AccountCreationUnavailable));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
