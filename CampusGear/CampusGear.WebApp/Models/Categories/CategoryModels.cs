using System.ComponentModel.DataAnnotations;
namespace CampusGear.Controllers;

public sealed class CategoryInput
{
    public Guid? Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [StringLength(1000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RowVersion { get; set; }
}
public sealed record CategoryRow(Guid Id, string Name, string? Description, bool IsActive, int Items);
public sealed record CategoriesPageModel(CategoryInput Input, IReadOnlyList<CategoryRow> Categories, string? Feedback,
    string Query, int Total);
