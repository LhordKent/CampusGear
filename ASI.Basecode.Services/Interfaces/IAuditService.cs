using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IAuditService
    {
        Task<AdminAuditPage> GetAuditAsync(string? q, int page = 1, CancellationToken cancellationToken = default);
        Task RecordAccountAsync(RequestContext actor, string action, string outcome, string? userId, CancellationToken ct);
    }
}
