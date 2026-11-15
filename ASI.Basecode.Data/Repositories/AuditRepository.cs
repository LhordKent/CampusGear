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
                    || x.ActorUser != null && (x.ActorUser.FullName.Contains(search) || x.ActorUser.Email!.Contains(search))
                    || (x.EntityType == "User" || x.EntityType == "ApplicationUser") && Context.Users.Any(u => u.Id == x.EntityId && (u.FullName.Contains(search) || u.Email!.Contains(search)))
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

        public async Task<IReadOnlyDictionary<string, AuditTarget>> GetTargetsAsync(IReadOnlyCollection<AuditEvent> events, CancellationToken ct)
        {
            var targets = new Dictionary<string, AuditTarget>();

            // Older events were recorded as "ApplicationUser", the user model's previous name.
            var userIds = Ids(events, nameof(User)).Concat(Ids(events, "ApplicationUser")).Distinct().ToList();
            foreach (var x in await Context.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).Select(x => new { x.Id, x.FullName }).ToListAsync(ct))
            {
                targets[$"{nameof(User)}:{x.Id}"] = new(x.FullName, null);
                targets[$"ApplicationUser:{x.Id}"] = new(x.FullName, null);
            }

            var categoryIds = Guids(events, nameof(EquipmentCategory));
            foreach (var x in await Context.EquipmentCategories.AsNoTracking().Where(x => categoryIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct))
            {
                targets[$"{nameof(EquipmentCategory)}:{x.Id}"] = new(x.Name, null);
            }

            var itemIds = Guids(events, nameof(EquipmentItem));
            foreach (var x in await Context.EquipmentItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.AssetCode }).ToListAsync(ct))
            {
                targets[$"{nameof(EquipmentItem)}:{x.Id}"] = new($"{x.Name} ({x.AssetCode})", null);
            }

            var caseIds = Guids(events, nameof(MaintenanceCase));
            foreach (var x in await Context.MaintenanceCases.AsNoTracking().Where(x => caseIds.Contains(x.Id)).Select(x => new { x.Id, x.EquipmentItem.Name, x.EquipmentItem.AssetCode }).ToListAsync(ct))
            {
                targets[$"{nameof(MaintenanceCase)}:{x.Id}"] = new($"{x.Name} ({x.AssetCode})", null);
            }

            var reservationIds = Guids(events, nameof(Reservation));
            foreach (
                var x in await Context
                    .Reservations.AsNoTracking()
                    .Where(x => reservationIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.EquipmentItem.Name, x.EquipmentItem.AssetCode, Borrower = x.BorrowerProfile.User.FullName })
                    .ToListAsync(ct)
            )
            {
                targets[$"{nameof(Reservation)}:{x.Id}"] = new($"{x.Name} ({x.AssetCode})", x.Borrower);
            }

            return targets;
        }

        private static List<string> Ids(IEnumerable<AuditEvent> events, string type)
        {
            return events.Where(x => x.EntityType == type).Select(x => x.EntityId).Distinct().ToList();
        }

        private static List<Guid> Guids(IEnumerable<AuditEvent> events, string type)
        {
            return events
                .Where(x => x.EntityType == type)
                .Select(x => Guid.TryParse(x.EntityId, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();
        }
    }
}
