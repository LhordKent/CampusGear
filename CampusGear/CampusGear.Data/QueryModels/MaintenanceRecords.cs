using CampusGear.Models;
namespace CampusGear.Data.QueryModels;
public sealed record PersonRecord(string FullName);
public sealed record MaintenanceRecord(Guid Id, Guid EquipmentItemId, MaintenanceStatus Status, string Description,
    string? Resolution, DateTime OpenedAtUtc, DateTime? ClosedAtUtc, byte[] RowVersion,
    EquipmentRecord EquipmentItem, PersonRecord OpenedByUser, PersonRecord? ClosedByUser);
public sealed record MaintenanceChoice(Guid Id, string Name, string AssetCode, EquipmentCondition Condition, bool HasCase, bool OnLoan);
public sealed record MaintenanceListData(IReadOnlyList<MaintenanceRecord> Cases, IReadOnlyList<MaintenanceChoice> Choices,
    int Page, int Pages, int Total, int OpenTotal, int ClosedTotal);
