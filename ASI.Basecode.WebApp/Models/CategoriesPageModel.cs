using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.Models
{
    public sealed record CategoriesPageModel(
        CategoryEditModel Input,
        IReadOnlyList<CategoryListItem> Categories,
        string? Feedback,
        string Query,
        int Total
    );
}
