using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ICurrentAccountService
    {
        Task<CurrentAccountDisplay> GetAsync(string? userId, string fallback);
    }
}
