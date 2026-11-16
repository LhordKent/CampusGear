using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>An equipment unit offered in the Open maintenance dialog; Blocked says why it cannot be chosen.</summary>
    public sealed record MaintenanceItemOption(
        Guid Id,
        string Name,
        string AssetCode,
        EquipmentCondition Condition,
        string? Blocked,
        string Category,
        string? Location,
        string? ImagePath
    )
    {
        /// <summary>Damaged or unserviceable but not yet in a case: listed first under "Needs attention".</summary>
        public bool NeedsAttention =>
            Blocked is null && Condition is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable;
    }

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
