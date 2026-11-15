using ASI.Basecode.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    // Retains Identity's EF implementation; only translates relational constraint failures at the boundary.
    public sealed class AccountUserStore : UserStore<User, IdentityRole, AsiBasecodeDBContext>
    {
        private readonly AsiBasecodeDBContext _db;
        private readonly IdentityErrorDescriber? _errors;

        public AccountUserStore(AsiBasecodeDBContext db, IdentityErrorDescriber? errors = null)
            : base(db, errors)
        {
            _db = db;
            _errors = errors;
        }

        public override Task<IdentityResult> CreateAsync(User user, CancellationToken ct = default)
        {
            return TranslateAsync(() => base.CreateAsync(user, ct));
        }

        public override Task<IdentityResult> UpdateAsync(User user, CancellationToken ct = default)
        {
            return TranslateAsync(() => base.UpdateAsync(user, ct));
        }

        public override Task<IdentityResult> DeleteAsync(User user, CancellationToken ct = default)
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
