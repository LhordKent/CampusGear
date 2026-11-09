using CampusGear.Controllers;
namespace CampusGear.Services;
public interface ICustodianService
{
    Task<CustodianDetailViewModel?> GetDetailsAsync(Guid id, bool custodian, CancellationToken ct);
    Task<CustodianCalendarViewModel> GetCalendarAsync(int? year, int? month, Guid? equipmentItemId, string? day, CancellationToken ct);
    Task<CustodianListViewModel> GetListAsync(string action, string title, string? search, string? status, string? from, string? to, int page, string queue, CancellationToken ct);
}
