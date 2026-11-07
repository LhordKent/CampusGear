using CampusGear.Models;
using CampusGear.Data.QueryModels;

namespace CampusGear.Data.Interfaces;
public interface IAuditRepository
{
    void Add(AuditEvent record);
    Task<AuditPageData> ListAsync(string search, int page, CancellationToken ct);
}
