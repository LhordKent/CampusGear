using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IRegistrationService
    {
        Task<OperationResult<RegistrationResult>> SignUpAsync(RequestContext actor, SignUpInput input, CancellationToken ct = default);
    }
}
