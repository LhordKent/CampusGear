using ASI.Basecode.Resources.Constants;

// EQUIPMENT ITEM (table: EquipmentItems)  -  ONE physical unit with its own label, e.g. "CAM-004 Canon EOS R50".
namespace ASI.Basecode.Data.Models
{
    public sealed class EquipmentItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CategoryId { get; set; }

        // The label code, e.g. CAM-004. Given once when the item is added; never changes.
        public string AssetCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? SerialNumber { get; set; }

        public string? Location { get; set; }

        // Web path of the uploaded photo under wwwroot (e.g. /uploads/equipment/{file}); null shows a placeholder.
        public string? ImagePath { get; set; }

        public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;

        // false = "inactive": hidden from students, history kept. (Delete does this for items with history.)
        public bool IsActive { get; set; } = true;

        // true = in maintenance (repair case open or Damaged): it can't be booked.
        public bool IsMaintenanceHold { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public EquipmentCategory Category { get; set; } = null!;

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

        public ICollection<MaintenanceCase> MaintenanceCases { get; set; } = new List<MaintenanceCase>();
    }
}
