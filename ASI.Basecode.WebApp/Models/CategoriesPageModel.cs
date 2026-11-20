using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.Models
{
    public sealed record CategoriesPageModel(
        CategoryEditModel Input,
        IReadOnlyList<CategoryListItem> Categories,
        string? Feedback,
        string Query,
        int Total,
        string? Error = null,
        // The asset code prefix of the category being edited, shown read-only in the editor.
        string? EditingPrefix = null
    );
}
