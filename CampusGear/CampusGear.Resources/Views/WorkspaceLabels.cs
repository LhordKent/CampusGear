using System.Resources;

namespace CampusGear.Resources.Views
{
    public static class WorkspaceLabels
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Views.WorkspaceLabels",
            typeof(WorkspaceLabels).Assembly
        );

        public static string Dashboard => Get(nameof(Dashboard));

        public static string Overview => Get(nameof(Overview));

        public static string EquipmentItems =>
            Resources.GetString(nameof(EquipmentItems)) ?? throw new MissingManifestResourceException(nameof(EquipmentItems));

        public static string Inventory => Get(nameof(Inventory));

        public static string EquipmentCategories =>
            Resources.GetString(nameof(EquipmentCategories)) ?? throw new MissingManifestResourceException(nameof(EquipmentCategories));

        public static string Maintenance => Get(nameof(Maintenance));

        public static string BorrowingHistory =>
            Resources.GetString(nameof(BorrowingHistory)) ?? throw new MissingManifestResourceException(nameof(BorrowingHistory));

        public static string Reservations => Get(nameof(Reservations));

        public static string AvailabilityCalendar =>
            Resources.GetString(nameof(AvailabilityCalendar)) ?? throw new MissingManifestResourceException(nameof(AvailabilityCalendar));

        public static string BorrowerProfiles =>
            Resources.GetString(nameof(BorrowerProfiles)) ?? throw new MissingManifestResourceException(nameof(BorrowerProfiles));

        public static string People => Get(nameof(People));

        public static string UsersRoles => Get(nameof(UsersRoles));

        public static string AuditLog => Get(nameof(AuditLog));

        public static string ApprovalRelease =>
            Resources.GetString(nameof(ApprovalRelease)) ?? throw new MissingManifestResourceException(nameof(ApprovalRelease));

        public static string Desk => Get(nameof(Desk));

        public static string ReturnConditionCheck =>
            Resources.GetString(nameof(ReturnConditionCheck)) ?? throw new MissingManifestResourceException(nameof(ReturnConditionCheck));

        public static string Records => Get(nameof(Records));

        public static string Directory => Get(nameof(Directory));

        public static string Home => Get(nameof(Home));

        public static string BrowseEquipment =>
            Resources.GetString(nameof(BrowseEquipment)) ?? throw new MissingManifestResourceException(nameof(BrowseEquipment));

        public static string MyReservations =>
            Resources.GetString(nameof(MyReservations)) ?? throw new MissingManifestResourceException(nameof(MyReservations));

        public static string Availability => Get(nameof(Availability));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
