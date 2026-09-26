namespace CampusGear.Models;

public sealed class MaintenanceCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EquipmentItemId { get; set; }
    public Guid? LoanId { get; set; }
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;
    public string Description { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public string OpenedByUserId { get; set; } = string.Empty;
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public EquipmentItem EquipmentItem { get; set; } = null!;
    public Loan? Loan { get; set; }
    public ApplicationUser OpenedByUser { get; set; } = null!;
    public ApplicationUser? ClosedByUser { get; set; }
}
