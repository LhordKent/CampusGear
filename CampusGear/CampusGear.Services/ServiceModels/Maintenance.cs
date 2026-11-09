using CampusGear.Data.QueryModels;
using CampusGear.Models;
namespace CampusGear.Services.ServiceModels;
public sealed record MaintenanceCommand(string? RowVersion, MaintenanceStatus Status, EquipmentCondition Condition, string? Resolution);
public sealed record MaintenancePage(MaintenanceListData Data, string Status, string Query);
