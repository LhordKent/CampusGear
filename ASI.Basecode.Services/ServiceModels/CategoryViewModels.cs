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

        public bool IsActive { get; set; } = true;

        public string? RowVersion { get; set; }
    }

    public sealed record CategoryListItem(Guid Id, string Name, string? Description, bool IsActive, int Items);

    public sealed record CategoryPage(IReadOnlyList<CategoryListItem> Categories, string Query, int Total);
}
