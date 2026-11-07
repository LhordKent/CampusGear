using CampusGear.Data.QueryModels;
using CampusGear.Models;
namespace CampusGear.Services.ServiceModels;
public sealed class EquipmentCommand
{
    public Guid? Id { get; set; }
    public Guid CategoryId { get; set; }
    public string AssetCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? SerialNumber { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public EquipmentCondition Condition { get; set; }
    public bool IsActive { get; set; }
    public string? RowVersion { get; set; }
    public bool RemoveImage { get; set; }
}
/// <summary>The controller owns/disposes the stream; the service coordinates storage and database compensation.</summary>
public sealed record UploadContent(Stream Content, long Length);
public sealed record InventoryPage(InventoryData Data, string Query);
