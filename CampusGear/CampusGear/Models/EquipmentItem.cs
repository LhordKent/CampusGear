namespace CampusGear.Models;

public sealed class EquipmentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SerialNumber { get; set; }
    public string? Location { get; set; }
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;
    public bool IsActive { get; set; } = true;
    public bool IsMaintenanceHold { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public EquipmentCategory Category { get; set; } = null!;
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<MaintenanceCase> MaintenanceCases { get; set; } = new List<MaintenanceCase>();
}
