using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public sealed class CategoryRepository : BaseRepository, ICategoryRepository
    {
        public CategoryRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        public Task<EquipmentCategory?> FindAsync(Guid id, CancellationToken ct)
        {
            return Context.EquipmentCategories.SingleOrDefaultAsync(x => x.Id == id, ct);
        }

        public async Task<CategoryList> ListAsync(string? search, CancellationToken ct)
        {
            var query = Context.EquipmentCategories.AsNoTracking();
            var total = await query.CountAsync(ct);
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x => x.Name.Contains(search) || x.Description != null && x.Description.Contains(search));
            }

            return new(
                await query
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .ThenBy(x => x.Name)
                    .Select(x => new CategoryListRecord(x.Id, x.Name, x.Description, x.IsActive, x.Items.Count, x.AssetCodeSeries!.Prefix))
                    .ToListAsync(ct),
                total
            );
        }

        public Task<bool> HasItemsAsync(Guid id, bool activeOnly, CancellationToken ct)
        {
            return Context.EquipmentItems.AnyAsync(x => x.CategoryId == id && (!activeOnly || x.IsActive), ct);
        }

        public Task<bool> IsActiveAsync(Guid id, CancellationToken ct)
        {
            return Context.EquipmentCategories.AsNoTracking().AnyAsync(x => x.Id == id && x.IsActive, ct);
        }

        public async Task<IReadOnlyList<EquipmentCategory>> ActiveAsync(CancellationToken ct)
        {
            return await Context.EquipmentCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(ct);
        }

        public void Add(EquipmentCategory category) => Context.EquipmentCategories.Add(category);

        public void Remove(EquipmentCategory category) => Context.EquipmentCategories.Remove(category);
    }
}
