using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class PasswordResetMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.PasswordResetMessages",
            typeof(PasswordResetMessages).Assembly
        );

        public static string RequestAccepted => Get(nameof(RequestAccepted));

        public static string CodeInvalid => Get(nameof(CodeInvalid));

        public static string ResetRequestExpired => Get(nameof(ResetRequestExpired));

        public static string CodeExpired => Get(nameof(CodeExpired));

        public static string CodeAttemptsExceeded => Get(nameof(CodeAttemptsExceeded));

        public static string CodeAlreadyUsed => Get(nameof(CodeAlreadyUsed));

        public static string ResetAuthorizationExpired => Get(nameof(ResetAuthorizationExpired));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
