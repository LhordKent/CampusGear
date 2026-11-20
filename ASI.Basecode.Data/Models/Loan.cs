using ASI.Basecode.Resources.Constants;

// LOAN (table: Loans)  -  created when the custodian hands an item over (Release).
// ReturnedAtUtc empty = the item is still out. Filled in on Return.
namespace ASI.Basecode.Data.Models
{
    public sealed class Loan
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ReservationId { get; set; }

        public DateTime ReleasedAtUtc { get; set; }

        // When the item must be back (the booking's end time).
        public DateTime DueAtUtc { get; set; }

        // Empty while the item is out on loan.
        public DateTime? ReturnedAtUtc { get; set; }

        public string ReleasedByUserId { get; set; } = string.Empty;

        public string? ReceivedByUserId { get; set; }

        public EquipmentCondition ConditionAtRelease { get; set; } = EquipmentCondition.Good;

        public EquipmentCondition? ConditionAtReturn { get; set; }

        public string? ReleaseNotes { get; set; }

        public string? ReturnNotes { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public Reservation Reservation { get; set; } = null!;

        public User ReleasedByUser { get; set; } = null!;

        public User? ReceivedByUser { get; set; }

        public ICollection<MaintenanceCase> MaintenanceCases { get; set; } = new List<MaintenanceCase>();
    }
}
