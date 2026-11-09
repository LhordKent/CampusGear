using CampusGear.Controllers;
namespace CampusGear.Services;
public interface IDashboardService
{
    Task<CustodianDashboardViewModel> GetCustodianAsync(string userId, CancellationToken ct);
    Task<AdminDashboardViewModel> GetAdministratorAsync(string userId, CancellationToken ct);
}
