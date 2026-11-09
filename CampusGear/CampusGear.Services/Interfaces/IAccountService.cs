using CampusGear.Services.ServiceModels;
namespace CampusGear.Services;
public interface IAccountService
{
    Task<OperationResult<AccountFlow>> GetLoginAsync(RequestContext actor, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> LoginAsync(RequestContext actor, LoginCommand input, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> SignUpAsync(RequestContext actor, SignUpCommand input, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> GetTwoFactorAsync(RequestContext actor, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> TwoFactorAsync(RequestContext actor, string? code, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> ResendTwoFactorAsync(RequestContext actor, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> PasswordResetRequestAsync(RequestContext actor, string emailInput, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> GetPasswordResetCodeAsync(RequestContext actor, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> PasswordResetCodeAsync(RequestContext actor, string? code, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> ResendPasswordResetCodeAsync(RequestContext actor, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> GetPasswordResetNewAsync(RequestContext actor, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> PasswordResetNewAsync(RequestContext actor, string? password, CancellationToken ct = default);
    Task<OperationResult<AccountFlow>> LogoutAsync(RequestContext actor, CancellationToken ct = default);
}
