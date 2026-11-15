using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IUserService
    {
        Task<AdminUsersPage> GetPageAsync(
            string? q,
            string? role,
            string? status,
            int page = 1,
            CancellationToken cancellationToken = default
        );
        Task<OperationResult<string>> DecideAsync(
            RequestContext actor,
            string id,
            AccountApprovalStatus decision,
            CancellationToken cancellationToken
        );
        Task<OperationResult<string>> EditAsync(RequestContext actor, string id, UserEditModel input, CancellationToken cancellationToken);
        Task<OperationResult<string>> CreateAsync(RequestContext actor, UserCreateModel input, CancellationToken cancellationToken);
        Task<OperationResult<string>> DeleteAsync(RequestContext actor, string id, CancellationToken cancellationToken);
        Task<UserEditPage?> GetEditAsync(RequestContext actor, string id, UserEditModel? input, CancellationToken cancellationToken);
    }
}
