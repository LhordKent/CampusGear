using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    public sealed class CategoryEditModel
    {
        public Guid? Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public string? RowVersion { get; set; }
    }

    public sealed record CategoryListItem(
        Guid Id,
        string Name,
        string? Description,
        int ActiveItems,
        int InactiveItems,
        string? AssetCodePrefix
    );

    public sealed record CategoryPage(IReadOnlyList<CategoryListItem> Categories, string Query, int Total);
}
