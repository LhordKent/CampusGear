using System.ComponentModel.DataAnnotations;
using CampusGear.Models;
using CampusGear.Data.QueryModels;
namespace CampusGear.Controllers;
public sealed class MaintenanceUpdateInput
{
    public string? RowVersion { get; set; }
    public MaintenanceStatus Status { get; set; }
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;
    [StringLength(2000)] public string? Resolution { get; set; }
}

/// <summary>An equipment unit offered in the Open maintenance dialog; Blocked says why it cannot be chosen.</summary>
public sealed record MaintenanceItemOption(Guid Id, string Name, string AssetCode, EquipmentCondition Condition, string? Blocked);

public sealed record MaintenancePageModel(IReadOnlyList<MaintenanceRecord> Cases, IReadOnlyList<MaintenanceItemOption> Items,
    string Status, string Query, int Page, int Pages, int Total, int OpenTotal, int ClosedTotal, string? Feedback, string Tone);
