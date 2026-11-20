using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.Models
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
        string? ImagePath,
        string? AssetCode
    );
}
