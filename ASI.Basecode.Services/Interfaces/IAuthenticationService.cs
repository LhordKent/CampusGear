using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAuthenticationService
    {
        Task<OperationResult<AuthenticationResult>> GetLoginAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<AuthenticationResult>> LoginAsync(RequestContext actor, LoginInput input, CancellationToken ct = default);
        Task<OperationResult<AuthenticationResult>> GetTwoFactorAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<AuthenticationResult>> TwoFactorAsync(RequestContext actor, string? code, CancellationToken ct = default);
        Task<OperationResult<AuthenticationResult>> ResendTwoFactorAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<AuthenticationResult>> LogoutAsync(RequestContext actor, CancellationToken ct = default);
    }
}
