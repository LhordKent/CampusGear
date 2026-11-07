namespace CampusGear.Models;
public sealed record EquipmentPickerItem(Guid Id, string Name, string AssetCode, string Category, string? ImagePath, ItemStatus Status);
