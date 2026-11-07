using CampusGear.Data.Interfaces;
using CampusGear.Models;

namespace CampusGear.Data.Repositories;
public sealed class AuditRepository(ApplicationDbContext db) : IAuditRepository
{
    public void Add(AuditEvent record) => db.AuditEvents.Add(record);
}
