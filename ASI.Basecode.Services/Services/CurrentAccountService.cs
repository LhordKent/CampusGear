using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;

namespace ASI.Basecode.Services.Services
{
    public sealed class CurrentAccountService : ServiceBase, ICurrentAccountService
    {
        private readonly UserManager<User> _userManager;

        public CurrentAccountService(UserManager<User> users)
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
