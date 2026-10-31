using CampusGear.Data;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Models;

/// <summary>One unit in the availability calendar's equipment picker (Views/Shared/_EquipmentPicker.cshtml).</summary>
public sealed record EquipmentPickerItem(Guid Id, string Name, string AssetCode, string Category, string? ImagePath, ItemStatus Status);

/// <summary>The picker for one calendar: its units, the one shown, and the calendar address and month it links to.</summary>
public sealed record EquipmentPickerModel(IReadOnlyList<EquipmentPickerItem> Items, Guid? SelectedId, string CalendarPath, int Year, int Month);

public static class EquipmentPicker
{
    /// <summary>Every active unit with its status right now, in the calendar's name order.</summary>
    public static async Task<IReadOnlyList<EquipmentPickerItem>> LoadAsync(ApplicationDbContext db, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var statuses = await ItemStatuses.ForAllAsync(db, nowUtc, cancellationToken);
        var items = await db.EquipmentItems.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name).ThenBy(x => x.AssetCode)
            .Select(x => new { x.Id, x.Name, x.AssetCode, Category = x.Category.Name, x.ImagePath })
            .ToListAsync(cancellationToken);
        return items.Select(x => new EquipmentPickerItem(x.Id, x.Name, x.AssetCode, x.Category, x.ImagePath,
            statuses.GetValueOrDefault(x.Id, ItemStatus.Available))).ToList();
    }
}
