using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.ServiceModels
{
    public sealed class EquipmentEditModel
    {
        public Guid? Id { get; set; }

        public Guid CategoryId { get; set; }

        [Required, StringLength(64)]
        public string AssetCode { get; set; } = "";

        [Required, StringLength(160)]
        public string Name { get; set; } = "";

        [StringLength(120)]
        public string? SerialNumber { get; set; }

        [StringLength(160)]
        public string? Location { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }

        public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;

        public bool IsActive { get; set; } = true;

        public string? RowVersion { get; set; }

        public bool RemoveImage { get; set; }
    }

    public sealed record EquipmentPickerItem(Guid Id, string Name, string AssetCode, string Category, string? ImagePath, ItemStatus Status);

    public sealed record InventoryPage(InventoryData Data, string Query);

    /// <summary>The controller owns/disposes the stream; the service coordinates storage and database compensation.</summary>
    public sealed record UploadContent(Stream Content, long Length);
}
