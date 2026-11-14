using CampusGear.Data.QueryModels;
using CampusGear.Resources.Constants;

namespace CampusGear.WebApp.Models
{
    /// <summary>An equipment unit offered in the Open maintenance dialog; Blocked says why it cannot be chosen.</summary>
    public sealed record MaintenanceItemOption(Guid Id, string Name, string AssetCode, EquipmentCondition Condition, string? Blocked);

    public sealed record MaintenancePageModel(
        IReadOnlyList<MaintenanceRecord> Cases,
        IReadOnlyList<MaintenanceItemOption> Items,
        string Status,
        string Query,
        int Page,
        int Pages,
        int Total,
        int OpenTotal,
        int ClosedTotal,
        string? Feedback,
        string Tone
    );
}
