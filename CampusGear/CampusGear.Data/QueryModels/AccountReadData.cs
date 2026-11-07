using CampusGear.Models;
namespace CampusGear.Data.QueryModels;
public sealed record UserRoleRecord(string UserId, string? Name);
public sealed record AccountPageData(IReadOnlyList<ApplicationUser> Accounts, IReadOnlyList<UserRoleRecord> Roles,
    int Page, int Pages, int Total, int Pending);
public sealed record AuditPageData(IReadOnlyList<AuditEvent> Events, int Page, int Pages, int Total);
