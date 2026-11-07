using System.ComponentModel.DataAnnotations;
using CampusGear.Models;
using CampusGear.Data.QueryModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace CampusGear.Controllers;
public sealed class EquipmentEditInput
{
    public Guid? Id { get; set; }
    public Guid CategoryId { get; set; }
    [Required, StringLength(64)] public string AssetCode { get; set; } = "";
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [StringLength(120)] public string? SerialNumber { get; set; }
    [StringLength(160)] public string? Location { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;
    public bool IsActive { get; set; } = true;
    public string? RowVersion { get; set; }
    // Display-only: the current photo is read from the database, never trusted from the form.
    [BindNever] public string? ImagePath { get; set; }
    public bool RemoveImage { get; set; }
}
public sealed record InventoryPageModel(EquipmentEditInput Input, IReadOnlyList<EquipmentRecord> Items,
    IReadOnlyList<CategoryRecord> Categories, string Query, int Page, int Pages, int Total, string? Feedback);
