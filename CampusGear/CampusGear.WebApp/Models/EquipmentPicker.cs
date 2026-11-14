using CampusGear.Services.ServiceModels;

namespace CampusGear.WebApp.Models
{
    public sealed record EquipmentPickerModel(
        IReadOnlyList<EquipmentPickerItem> Items,
        Guid? SelectedId,
        string CalendarPath,
        int Year,
        int Month
    );
}
