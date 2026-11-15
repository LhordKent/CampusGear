using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public sealed class AuditRepository : BaseRepository, IAuditRepository
    {
        public AuditRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        public void Add(AuditEvent record) => Context.AuditEvents.Add(record);

        public async Task<AuditPageData> ListAsync(string search, int page, CancellationToken ct)
        {
            var query = Context.AuditEvents.AsNoTracking();
            if (search.Length > 0)
            {
                query = query.Where(x =>
                    x.Action.Contains(search)
                    || x.EntityType.Contains(search)
                    || x.EntityId.Contains(search)
                    || x.Outcome.Contains(search)
                    || x.IpAddress != null && x.IpAddress.Contains(search)
                    || x.ActorUser != null && (x.ActorUser.FullName.Contains(search) || x.ActorUser.Email!.Contains(search))
                );
            }

            var total = await query.CountAsync(ct);
            var pages = Math.Max(1, (int)Math.Ceiling(total / 25d));
            page = Math.Clamp(page, 1, pages);
            return new(
                await query
                    .Include(x => x.ActorUser)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .ThenByDescending(x => x.Id)
                    .Skip((page - 1) * 25)
                    .Take(25)
                    .ToListAsync(ct),
                page,
                pages,
                total
            );
        }
    }
}
