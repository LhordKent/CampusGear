using CampusGear.Controllers;
using CampusGear.Services.ServiceModels;
namespace CampusGear.Services;
public sealed record AvailabilityResult(int BookableUnits, int FreeUnits, int MaxQuantity);
public interface IBorrowerService
{
    Task<OperationResult<BorrowerHomeViewModel>> HomeAsync(RequestContext actor, CancellationToken ct);
    Task<OperationResult<ReservationPageViewModel>> ReservationAsync(RequestContext actor, Guid? equipmentItemId, string? state, Guid[]? id, CancellationToken ct);
    Task<OperationResult<AvailabilityResult>> AvailabilityAsync(RequestContext actor, Guid equipmentItemId, string? start, string? end, CancellationToken ct);
    Task<OperationResult<ReservationPageViewModel>> CreateReservationAsync(RequestContext actor, ReservationInputModel input, CancellationToken ct);
    Task<OperationResult<HistoryPageViewModel>> HistoryAsync(RequestContext actor, string? status, string? from, string? to, string? q, int page, CancellationToken ct);
    Task<OperationResult<CalendarPageViewModel>> CalendarAsync(RequestContext actor, int? year, int? month, Guid? equipmentItemId, string? day, CancellationToken ct);
}
