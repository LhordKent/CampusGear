namespace ASI.Basecode.Data.Models
{
    public sealed record CategoryListRecord(
        Guid Id,
        string Name,
        string? Description,
        int ActiveItems,
        int InactiveItems,
        string? AssetCodePrefix
    );

    public sealed record CategoryList(IReadOnlyList<CategoryListRecord> Rows, int Total);
}
