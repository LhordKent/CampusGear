namespace CampusGear.Models;

public sealed class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; } = Guid.NewGuid();
    public Guid BorrowerProfileId { get; set; }
    public Guid EquipmentItemId { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public DateTime? HoldExpiresAtUtc { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public string Purpose { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? DecisionReason { get; set; }
    public string? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public BorrowerProfile BorrowerProfile { get; set; } = null!;
    public EquipmentItem EquipmentItem { get; set; } = null!;
    public ApplicationUser? DecidedByUser { get; set; }
    public Loan? Loan { get; set; }
}
