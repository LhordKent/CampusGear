using CampusGear.Models;

namespace CampusGear.Data.Interfaces;
public interface IAuditRepository
{
    void Add(AuditEvent record);
}
