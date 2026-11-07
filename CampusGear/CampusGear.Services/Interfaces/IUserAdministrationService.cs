using CampusGear.Controllers;
using CampusGear.Services.ServiceModels;
using CampusGear.Models;
namespace CampusGear.Services;
public interface IUserAdministrationService
{
 Task<AdminUsersPage> GetPageAsync(string? q,string? role,string? status,int page,CancellationToken ct);
 Task<OperationResult<string>> DecideAsync(RequestContext actor,string id,AccountApprovalStatus decision,CancellationToken ct);
 Task<OperationResult<string>> EditAsync(RequestContext actor,string id,UserEditCommand input,CancellationToken ct);
 Task<OperationResult<string>> CreateAsync(RequestContext actor,UserCreateCommand input,CancellationToken ct);
 Task<OperationResult<string>> DeleteAsync(RequestContext actor,string id,CancellationToken ct);
 Task<AdminAuditPage> GetAuditAsync(string? q,int page,CancellationToken ct);
 Task<UserEditPage?> GetEditAsync(RequestContext actor,string id,UserEditCommand? input,CancellationToken ct);
}
