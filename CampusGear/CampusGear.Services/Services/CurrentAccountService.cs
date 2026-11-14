using CampusGear.Data.Models;
using CampusGear.Services.Interfaces;
using CampusGear.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;

namespace CampusGear.Services.Services
{
    public sealed class CurrentAccountService : ICurrentAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public CurrentAccountService(UserManager<ApplicationUser> users)
        {
            _userManager = users;
        }

        public async Task<CurrentAccountDisplay> GetAsync(string? userId, string fallback)
        {
            var account = userId is null ? null : await _userManager.FindByIdAsync(userId);
            var name = account?.FullName ?? fallback;
            return new(name, name.Length > 0 ? name[..1].ToUpperInvariant() : "C");
        }
    }
}
