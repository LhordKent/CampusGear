using CampusGear.Models;
namespace CampusGear.Controllers;
public sealed record AdminUserRow(string Id, string FullName, string Email, string Role,
    bool IsActive, AccountApprovalStatus ApprovalStatus, string? StudentNumber, string? Department, bool? IsEligible,
    string RegisteredManila);
public sealed record AdminUsersPage(IReadOnlyList<AdminUserRow> Rows, string Query, string Role,
    string Status, int Page, int PageCount, int Total, int PendingTotal, string? Feedback, string? Error);

public sealed record AdminAuditRow(Guid Id, string TimeManila, string Action, string ActorName,
    string? ActorEmail, string EntityType, string EntityId, string Outcome, string? IpAddress,
    string? CorrelationId, string? BeforeJson, string? AfterJson);
public sealed record AdminAuditPage(IReadOnlyList<AdminAuditRow> Rows, string Query, int Page,
    int PageCount, int Total);
