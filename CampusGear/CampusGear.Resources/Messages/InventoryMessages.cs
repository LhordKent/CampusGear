using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class InventoryMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.InventoryMessages",
            typeof(InventoryMessages).Assembly
        );

        public static string InvalidCondition => Get(nameof(InvalidCondition));

        public static string ActiveCategoryRequired => Get(nameof(ActiveCategoryRequired));

        public static string ImageTooLarge => Get(nameof(ImageTooLarge));

        public static string InactiveCategory => Get(nameof(InactiveCategory));

        public static string StaleEquipment => Get(nameof(StaleEquipment));

        public static string ActiveLoanPreventsEdit => Get(nameof(ActiveLoanPreventsEdit));

        public static string LiveReservationsPreventDeactivation => Get(nameof(LiveReservationsPreventDeactivation));

        public static string RecordBusy => Get(nameof(RecordBusy));

        public static string DuplicateOrStaleEquipment => Get(nameof(DuplicateOrStaleEquipment));

        public static string EquipmentAdded => Get(nameof(EquipmentAdded));

        public static string EquipmentUpdated => Get(nameof(EquipmentUpdated));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
