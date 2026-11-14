using System.Resources;

namespace CampusGear.Resources.Messages
{
    public static class ReservationMessages
    {
        private static readonly ResourceManager Resources = new(
            "CampusGear.Resources.Messages.ReservationMessages",
            typeof(ReservationMessages).Assembly
        );

        public static string PurposeRequired => Get(nameof(PurposeRequired));

        public static string InvalidBookingInput => Get(nameof(InvalidBookingInput));

        public static string BorrowerOwnershipRequired => Get(nameof(BorrowerOwnershipRequired));

        public static string IneligibleBorrower => Get(nameof(IneligibleBorrower));

        public static string StartTimePassed => Get(nameof(StartTimePassed));

        public static string BorrowerProfileRequired => Get(nameof(BorrowerProfileRequired));

        public static string ScheduleUnavailable => Get(nameof(ScheduleUnavailable));

        public static string AlreadySubmitted => Get(nameof(AlreadySubmitted));

        public static string PendingApprovalRequired => Get(nameof(PendingApprovalRequired));

        public static string ScheduleConflict => Get(nameof(ScheduleConflict));

        public static string RejectionReasonRequired => Get(nameof(RejectionReasonRequired));

        public static string PendingRejectionRequired => Get(nameof(PendingRejectionRequired));

        public static string CancellationForbidden => Get(nameof(CancellationForbidden));

        public static string CancellationUnavailable => Get(nameof(CancellationUnavailable));

        public static string DamagedReleaseForbidden => Get(nameof(DamagedReleaseForbidden));

        public static string ApprovalRequired => Get(nameof(ApprovalRequired));

        public static string ReleaseOutsideBooking => Get(nameof(ReleaseOutsideBooking));

        public static string EquipmentUnavailable => Get(nameof(EquipmentUnavailable));

        public static string InvalidReturnCondition => Get(nameof(InvalidReturnCondition));

        public static string ActiveLoanRequired => Get(nameof(ActiveLoanRequired));

        public static string ReturnBeforeRelease => Get(nameof(ReturnBeforeRelease));

        public static string InvalidBatchSize => Get(nameof(InvalidBatchSize));

        public static string RecordBusy => Get(nameof(RecordBusy));

        public static string BorrowerNoLongerEligible => Get(nameof(BorrowerNoLongerEligible));

        public static string BorrowerRoleRequired => Get(nameof(BorrowerRoleRequired));

        public static string ReservationIdRequired => Get(nameof(ReservationIdRequired));

        public static string ReservationNotFound => Get(nameof(ReservationNotFound));

        public static string SignInRequired => Get(nameof(SignInRequired));

        public static string VerifiedAccountRequired => Get(nameof(VerifiedAccountRequired));

        public static string CustodianRequired => Get(nameof(CustodianRequired));

        public static string StaleRecord => Get(nameof(StaleRecord));

        public static string DuplicateRequestId => Get(nameof(DuplicateRequestId));

        public static string ItemNotBookable => Get(nameof(ItemNotBookable));

        private static string Get(string key)
        {
            return Resources.GetString(key) ?? throw new MissingManifestResourceException(key);
        }
    }
}
