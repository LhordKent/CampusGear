namespace ASI.Basecode.Data.Models
{
    public sealed class EquipmentCategory
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public AssetCodeSeries? AssetCodeSeries { get; set; }

        public ICollection<EquipmentItem> Items { get; set; } = new List<EquipmentItem>();
    }
}
