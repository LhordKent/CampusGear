// EQUIPMENT CATEGORY (table: EquipmentCategories)  -  a group like "Cameras" or "Laptops".
namespace ASI.Basecode.Data.Models
{
    public sealed class EquipmentCategory
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        // false = deleted, but kept because inactive items still point to it. Hidden everywhere.
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public AssetCodeSeries? AssetCodeSeries { get; set; }

        public ICollection<EquipmentItem> Items { get; set; } = new List<EquipmentItem>();
    }
}
