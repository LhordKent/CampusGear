using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Models
{
    public sealed record CategoryRecord(Guid Id, string Name, string? Description, bool IsActive, string? AssetCodePrefix = null);

    /// <summary>What stops an item from being edited or made inactive right now; shown in the editor before saving.</summary>
    public sealed record EquipmentEditState(DateTime? LoanDueAtUtc, int UpcomingBookings)
    {
        public bool OnLoan => LoanDueAtUtc is not null;
    }

    public sealed record EquipmentRecord(
        Guid Id,
        Guid CategoryId,
        string AssetCode,
        string Name,
        string? Description,
        string? SerialNumber,
        string? Location,
        string? ImagePath,
        EquipmentCondition Condition,
        bool IsActive,
        bool IsMaintenanceHold,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        byte[] RowVersion,
        CategoryRecord Category
    )
    {
        public static EquipmentRecord From(EquipmentItem x)
        {
            return new(
                x.Id,
                x.CategoryId,
                x.AssetCode,
                x.Name,
                x.Description,
                x.SerialNumber,
                x.Location,
                x.ImagePath,
                x.Condition,
                x.IsActive,
                x.IsMaintenanceHold,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.RowVersion.ToArray(),
                new(x.CategoryId, x.Category?.Name ?? "", x.Category?.Description, x.Category?.IsActive ?? true)
            );
        }
    }

    public sealed record InventoryData(
        IReadOnlyList<EquipmentRecord> Items,
        IReadOnlyList<CategoryRecord> Categories,
        int Page,
        int Pages,
        int Total,
        // Storage locations already in use, suggested in the editor so the same room is spelled the same way.
        IReadOnlyList<string>? Locations = null
    );
}
