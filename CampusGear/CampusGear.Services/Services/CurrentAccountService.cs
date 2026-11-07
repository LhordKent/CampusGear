using CampusGear.Models;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;
namespace CampusGear.Services;
public sealed class CurrentAccountService(UserManager<ApplicationUser> users) : ICurrentAccountService
{
 public async Task<CurrentAccountDisplay> GetAsync(string? userId,string fallback)
 {
 var account=userId is null?null:await users.FindByIdAsync(userId);
 var name=account?.FullName??fallback;
 return new(name,name.Length>0?name[..1].ToUpperInvariant():"C");
 }
}
