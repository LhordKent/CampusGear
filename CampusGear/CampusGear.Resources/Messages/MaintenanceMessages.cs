using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class MaintenanceMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.MaintenanceMessages",
            typeof(MaintenanceMessages).Assembly
        );

        public static string EquipmentRequired => Get(nameof(EquipmentRequired));

        public static string DescriptionRequired => Get(nameof(DescriptionRequired));

        public static string DescriptionTooLong => Get(nameof(DescriptionTooLong));

        public static string EquipmentUnavailable => Get(nameof(EquipmentUnavailable));

        public static string ActiveLoan => Get(nameof(ActiveLoan));

        public static string OpenCaseExists => Get(nameof(OpenCaseExists));

        public static string RecordBusy => Get(nameof(RecordBusy));

        public static string StaleRecord => Get(nameof(StaleRecord));

        public static string InvalidEdit => Get(nameof(InvalidEdit));

        public static string RepairDescriptionRequired => Get(nameof(RepairDescriptionRequired));

        public static string InvalidClosingCondition => Get(nameof(InvalidClosingCondition));

        public static string CaseNotFound => Get(nameof(CaseNotFound));

        public static string StaleCase => Get(nameof(StaleCase));

        public static string CaseAlreadyClosed => Get(nameof(CaseAlreadyClosed));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
