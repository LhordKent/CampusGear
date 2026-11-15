using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IAuditRepository
    {
        void Add(AuditEvent record);
        Task<AuditPageData> ListAsync(string search, int page, CancellationToken ct);
    }
}
