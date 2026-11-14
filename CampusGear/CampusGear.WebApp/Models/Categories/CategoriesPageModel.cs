using CampusGear.Services.ServiceModels;

namespace CampusGear.WebApp.Models
{
    public sealed record CategoriesPageModel(
        CategoryEditModel Input,
        IReadOnlyList<CategoryListItem> Categories,
        string? Feedback,
        string Query,
        int Total
    );
}
