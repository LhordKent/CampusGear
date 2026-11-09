using CampusGear.Data.QueryModels;
namespace CampusGear.Services.ServiceModels;
public sealed record CategoryCommand(Guid? Id, string Name, string? Description, bool IsActive, string? RowVersion);
public sealed record CategoryPage(IReadOnlyList<CategoryListRecord> Categories, string Query, int Total);
