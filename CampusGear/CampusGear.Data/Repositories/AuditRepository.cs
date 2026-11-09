using CampusGear.Data.Interfaces;
using CampusGear.Models;
using CampusGear.Data.QueryModels;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Data.Repositories;
public sealed class AuditRepository(ApplicationDbContext db) : IAuditRepository
{
    public void Add(AuditEvent record) => db.AuditEvents.Add(record);
    public async Task<AuditPageData> ListAsync(string search, int page, CancellationToken ct)
    {
        var query = db.AuditEvents.AsNoTracking();
        if (search.Length > 0) query = query.Where(x => x.Action.Contains(search) || x.EntityType.Contains(search) || x.EntityId.Contains(search) ||
            x.Outcome.Contains(search) || x.IpAddress != null && x.IpAddress.Contains(search) || x.ActorUser != null &&
            (x.ActorUser.FullName.Contains(search) || x.ActorUser.Email!.Contains(search)));
        var total = await query.CountAsync(ct); var pages = Math.Max(1, (int)Math.Ceiling(total / 25d)); page = Math.Clamp(page, 1, pages);
        return new(await query.Include(x => x.ActorUser).OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * 25).Take(25).ToListAsync(ct), page, pages, total);
    }
}
