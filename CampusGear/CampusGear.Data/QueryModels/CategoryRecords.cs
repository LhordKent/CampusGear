namespace CampusGear.Data.QueryModels;
public sealed record CategoryListRecord(Guid Id, string Name, string? Description, bool IsActive, int Items);
public sealed record CategoryList(IReadOnlyList<CategoryListRecord> Rows, int Total);
