using CampusGear.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using CampusGear.Data.Interfaces;
namespace CampusGear.Data;
// Retains Identity's EF implementation; only translates relational constraint failures at the boundary.
public sealed class AccountUserStore(ApplicationDbContext db,IdentityErrorDescriber? errors=null)
 : UserStore<ApplicationUser,IdentityRole,ApplicationDbContext>(db,errors)
{
 public override Task<IdentityResult> CreateAsync(ApplicationUser user,CancellationToken ct=default)=>TranslateAsync(()=>base.CreateAsync(user,ct));
 public override Task<IdentityResult> UpdateAsync(ApplicationUser user,CancellationToken ct=default)=>TranslateAsync(()=>base.UpdateAsync(user,ct));
 public override Task<IdentityResult> DeleteAsync(ApplicationUser user,CancellationToken ct=default)=>TranslateAsync(()=>base.DeleteAsync(user,ct));
 private static async Task<IdentityResult> TranslateAsync(Func<Task<IdentityResult>> write)
 {
 try{return await write();}
 catch(DbUpdateException e)when(e.InnerException is SqlException {Number:2601 or 2627} sql)
 {throw new PersistenceException(PersistenceFailure.Duplicate,sql.Message.Contains("StudentNumber",StringComparison.OrdinalIgnoreCase)?"StudentNumber":null);}
 }
}
