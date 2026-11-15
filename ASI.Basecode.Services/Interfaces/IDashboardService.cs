using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<CustodianDashboardViewModel> GetCustodianAsync(string userId, CancellationToken ct);
        Task<AdminDashboardViewModel> GetAdministratorAsync(string userId, CancellationToken ct);
    }
}
