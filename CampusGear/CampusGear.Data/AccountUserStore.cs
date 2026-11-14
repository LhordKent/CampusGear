using CampusGear.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Data
{
    // Retains Identity's EF implementation; only translates relational constraint failures at the boundary.
    public sealed class AccountUserStore : UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>
    {
        private readonly ApplicationDbContext _db;
        private readonly IdentityErrorDescriber? _errors;

        public AccountUserStore(ApplicationDbContext db, IdentityErrorDescriber? errors = null)
            : base(db, errors)
        {
            _db = db;
            _errors = errors;
        }

        public override Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct = default)
        {
            return TranslateAsync(() => base.CreateAsync(user, ct));
        }

        public override Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct = default)
        {
            return TranslateAsync(() => base.UpdateAsync(user, ct));
        }

        public override Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct = default)
        {
            return TranslateAsync(() => base.DeleteAsync(user, ct));
        }

        private static async Task<IdentityResult> TranslateAsync(Func<Task<IdentityResult>> write)
        {
            try
            {
                return await write();
            }
            catch (DbUpdateException error)
            {
                throw PersistenceErrors.Translate(error);
            }
        }
    }
}
