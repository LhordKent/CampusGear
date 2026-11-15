using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IAuditRepository
    {
        void Add(AuditEvent record);
        Task<AuditPageData> ListAsync(string search, int page, CancellationToken ct);

        /// <summary>Names for the records the events are about, keyed "EntityType:EntityId". Deleted records are missing.</summary>
        Task<IReadOnlyDictionary<string, AuditTarget>> GetTargetsAsync(IReadOnlyCollection<AuditEvent> events, CancellationToken ct);
    }
}
