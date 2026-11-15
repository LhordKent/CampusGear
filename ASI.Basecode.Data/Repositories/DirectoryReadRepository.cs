using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public sealed class DirectoryReadRepository : BaseRepository, IDirectoryReadRepository
    {
        public DirectoryReadRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        public async Task<IReadOnlyList<DirectoryCategoryData>> CategoriesAsync(CancellationToken ct)
        {
            return await Context
                .EquipmentCategories.AsNoTracking()
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenBy(x => x.Name)
                .Select(x => new DirectoryCategoryData(
                    x.Name,
                    x.Description,
                    x.IsActive,
                    x.Items.OrderByDescending(i => i.CreatedAtUtc)
                        .ThenBy(i => i.AssetCode)
                        .Select(i => new DirectoryUnitData(i.Id, i.Name, i.AssetCode, i.Condition, i.Location))
                        .ToList()
                ))
                .ToListAsync(ct);
        }

        public async Task<DirectoryPageData> BorrowersAsync(string search, string eligibility, int page, CancellationToken ct)
        {
            var query = Context
                .BorrowerProfiles.AsNoTracking()
                .Where(x =>
                    Context.UserRoles.Any(ur =>
                        ur.UserId == x.UserId && Context.Roles.Any(r => r.Id == ur.RoleId && r.Name == RoleNames.Borrower)
                    )
                );
            if (search.Length > 0)
            {
                query = query.Where(x =>
                    x.User.FullName.Contains(search)
                    || x.User.Email!.Contains(search)
                    || x.StudentNumber != null && x.StudentNumber.Contains(search)
                    || x.Department != null && x.Department.Contains(search)
                );
            }

            if (eligibility == "eligible")
            {
                query = query.Where(x => x.IsEligible);
            }

            if (eligibility == "paused")
            {
                query = query.Where(x => !x.IsEligible);
            }

            var total = await query.CountAsync(ct);
            var pages = Math.Max(1, (int)Math.Ceiling(total / 15d));
            page = Math.Clamp(page, 1, pages);
            return new(
                await query
                    .OrderByDescending(x => x.User.CreatedAtUtc)
                    .ThenBy(x => x.User.FullName)
                    .Skip((page - 1) * 15)
                    .Take(15)
                    .Select(x => new DirectoryBorrowerData(
                        x.User.FullName,
                        x.User.Email ?? "",
                        x.StudentNumber,
                        x.Department,
                        x.ContactNumber,
                        x.IsEligible,
                        x.EligibilityNotes,
                        x.User.IsActive
                    ))
                    .ToListAsync(ct),
                page,
                pages,
                total
            );
        }
    }
}
