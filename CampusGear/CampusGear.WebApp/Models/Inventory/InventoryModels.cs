using CampusGear.Data.QueryModels;
using CampusGear.Services.ServiceModels;

namespace CampusGear.WebApp.Models
{
    public sealed record InventoryPageModel(
        EquipmentEditModel Input,
        IReadOnlyList<EquipmentRecord> Items,
        IReadOnlyList<CategoryRecord> Categories,
        string Query,
        int Page,
        int Pages,
        int Total,
        string? Feedback,
        string? ImagePath = null
    );
}
