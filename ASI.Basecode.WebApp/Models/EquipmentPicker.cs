using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.Models
{
    public sealed record EquipmentPickerModel(
        IReadOnlyList<EquipmentPickerItem> Items,
        Guid? SelectedId,
        string CalendarPath,
        int Year,
        int Month
    );
}
