using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class AuthenticationMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.AuthenticationMessages",
            typeof(AuthenticationMessages).Assembly
        );

        public static string InvalidCredentials => Get(nameof(InvalidCredentials));

        public static string PendingApproval => Get(nameof(PendingApproval));

        public static string RegistrationRejected => Get(nameof(RegistrationRejected));

        public static string AccountLocked => Get(nameof(AccountLocked));

        public static string AdministratorCodeDeliveryFailed => Get(nameof(AdministratorCodeDeliveryFailed));

        public static string CodeCooldown => Get(nameof(CodeCooldown));

        public static string AdministratorCodeSent => Get(nameof(AdministratorCodeSent));

        public static string CodeResent => Get(nameof(CodeResent));

        public static string ResendCooldown => Get(nameof(ResendCooldown));

        public static string CodeDeliveryFailed => Get(nameof(CodeDeliveryFailed));

        public static string CodeExpired => Get(nameof(CodeExpired));

        public static string CodeAttemptsExceeded => Get(nameof(CodeAttemptsExceeded));

        public static string CodeInvalid => Get(nameof(CodeInvalid));

        public static string SignInExpired => Get(nameof(SignInExpired));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
