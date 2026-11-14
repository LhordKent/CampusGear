using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class UserMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.UserMessages",
            typeof(UserMessages).Assembly
        );

        public static string StaleAccountPage => Get(nameof(StaleAccountPage));

        public static string InvalidRole => Get(nameof(InvalidRole));

        public static string StaleAccountEdit => Get(nameof(StaleAccountEdit));

        public static string SelfAccessChangeForbidden => Get(nameof(SelfAccessChangeForbidden));

        public static string StaffApprovalRequired => Get(nameof(StaffApprovalRequired));

        public static string LastAdministratorRequired => Get(nameof(LastAdministratorRequired));

        public static string BorrowerProfileRoleRequired => Get(nameof(BorrowerProfileRoleRequired));

        public static string StudentNumberAlreadyRegistered => Get(nameof(StudentNumberAlreadyRegistered));

        public static string AccountUpdated => Get(nameof(AccountUpdated));

        public static string BorrowerFieldsOnly => Get(nameof(BorrowerFieldsOnly));

        public static string EmailAlreadyRegistered => Get(nameof(EmailAlreadyRegistered));

        public static string RoleAssignmentFailed => Get(nameof(RoleAssignmentFailed));

        public static string SelfDeletionForbidden => Get(nameof(SelfDeletionForbidden));

        public static string DeletionFailed => Get(nameof(DeletionFailed));

        public static string AdministrationBusy => Get(nameof(AdministrationBusy));

        public static string UpdateFailed => Get(nameof(UpdateFailed));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
