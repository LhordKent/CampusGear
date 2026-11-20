using ASI.Basecode.Resources.Constants;

// RESERVATION (table: Reservations)  -  one booking of ONE equipment item by ONE student for START..END.
// Its Status moves Pending -> Approved -> Released -> Completed (see ReservationService).
namespace ASI.Basecode.Data.Models
{
    public sealed class Reservation
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Id of the booking form submission; used to spot the same form being sent twice.
        public Guid RequestId { get; set; } = Guid.NewGuid();

        public Guid BorrowerProfileId { get; set; }

        public Guid EquipmentItemId { get; set; }

        public DateTime StartAtUtc { get; set; }

        public DateTime EndAtUtc { get; set; }

        // Pending only: when the request expires if no custodian has decided (24 h, or the start time if sooner).
        public DateTime? HoldExpiresAtUtc { get; set; }

        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

        public string Purpose { get; set; } = string.Empty;

        public string? Notes { get; set; }

        // Rejection reason, or the reason it was cancelled automatically.
        public string? DecisionReason { get; set; }

        // The custodian who approved or rejected it.
        public string? DecidedByUserId { get; set; }

        public DateTime? DecidedAtUtc { get; set; }

        public DateTime? CancelledAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Changes on every save; used to detect two people editing the same row at once.
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public BorrowerProfile BorrowerProfile { get; set; } = null!;

        public EquipmentItem EquipmentItem { get; set; } = null!;

        public User? DecidedByUser { get; set; }

        // Filled in once the item is released (handed over).
        public Loan? Loan { get; set; }
    }
}
