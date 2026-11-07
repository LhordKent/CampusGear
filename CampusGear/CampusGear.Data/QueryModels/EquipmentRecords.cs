using CampusGear.Models;
namespace CampusGear.Data.QueryModels;
public sealed record CategoryRecord(Guid Id, string Name, string? Description, bool IsActive);
public sealed record EquipmentRecord(Guid Id, Guid CategoryId, string AssetCode, string Name, string? Description,
    string? SerialNumber, string? Location, string? ImagePath, EquipmentCondition Condition,
    bool IsActive, bool IsMaintenanceHold, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, byte[] RowVersion, CategoryRecord Category)
{
    public static EquipmentRecord From(EquipmentItem x) => new(x.Id, x.CategoryId, x.AssetCode, x.Name, x.Description,
        x.SerialNumber, x.Location, x.ImagePath, x.Condition, x.IsActive, x.IsMaintenanceHold, x.CreatedAtUtc, x.UpdatedAtUtc,
        x.RowVersion.ToArray(), new(x.CategoryId, x.Category?.Name ?? "", x.Category?.Description, x.Category?.IsActive ?? true));
}
public sealed record InventoryData(IReadOnlyList<EquipmentRecord> Items, IReadOnlyList<CategoryRecord> Categories,
    int Page, int Pages, int Total);
