using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class ReservationRequestMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.ReservationRequestMessages",
            typeof(ReservationRequestMessages).Assembly
        );

        public static string BorrowingPaused => Get(nameof(BorrowingPaused));

        public static string InvalidRequestId => Get(nameof(InvalidRequestId));

        public static string EquipmentRequired => Get(nameof(EquipmentRequired));

        public static string SingleUnitOnly => Get(nameof(SingleUnitOnly));

        public static string PurposeRequired => Get(nameof(PurposeRequired));

        public static string PurposeTooLong => Get(nameof(PurposeTooLong));

        public static string NotesTooLong => Get(nameof(NotesTooLong));

        public static string InvalidDateRange => Get(nameof(InvalidDateRange));

        public static string FutureStartRequired => Get(nameof(FutureStartRequired));

        public static string ReusedRequestForm => Get(nameof(ReusedRequestForm));

        public static string ScheduleUnavailable => Get(nameof(ScheduleUnavailable));

        public static string ConcurrentBooking => Get(nameof(ConcurrentBooking));

        public static string BatchWithdrawn => Get(nameof(BatchWithdrawn));

        public static string InvalidManilaDate => Get(nameof(InvalidManilaDate));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
