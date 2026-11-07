namespace CampusGear.Models;
public sealed record EquipmentPickerModel(IReadOnlyList<EquipmentPickerItem> Items, Guid? SelectedId, string CalendarPath, int Year, int Month);
