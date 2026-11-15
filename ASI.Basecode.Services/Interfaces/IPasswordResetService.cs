using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IPasswordResetService
    {
        Task<OperationResult<PasswordResetResult>> PasswordResetRequestAsync(
            RequestContext actor,
            string emailInput,
            CancellationToken ct = default
        );
        Task<OperationResult<PasswordResetResult>> GetPasswordResetCodeAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<PasswordResetResult>> PasswordResetCodeAsync(
            RequestContext actor,
            string? code,
            CancellationToken ct = default
        );
        Task<OperationResult<PasswordResetResult>> ResendPasswordResetCodeAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<PasswordResetResult>> GetPasswordResetNewAsync(RequestContext actor, CancellationToken ct = default);
        Task<OperationResult<PasswordResetResult>> PasswordResetNewAsync(
            RequestContext actor,
            string? password,
            CancellationToken ct = default
        );
    }
}
