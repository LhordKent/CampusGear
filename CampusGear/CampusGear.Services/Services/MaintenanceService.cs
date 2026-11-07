using System.Text.Json;
using CampusGear.Data.Interfaces;
using CampusGear.Models;
using CampusGear.Services.ServiceModels;
namespace CampusGear.Services;
public sealed class MaintenanceService(IMaintenanceRepository cases, IEquipmentRepository equipment,
    IUnitOfWork work, IWorkflowLockManager locks, IAuditRepository audits, TimeProvider clock) : IMaintenanceService
{
    public async Task<MaintenancePage> GetPageAsync(string? status, string? query, int page, CancellationToken ct)
    {
        status = status == "closed" ? "closed" : "open";
        query = query?.Trim();
        if (query?.Length > 120) query = query[..120];
        return new(await cases.ListAsync(status, query, page, ct), status, query ?? "");
    }
    public async Task<OperationResult<bool>> OpenAsync(RequestContext actor, Guid equipmentItemId, string? description, CancellationToken cancellationToken)
    {
        description = description?.Trim();
        if (equipmentItemId == Guid.Empty)
            return Outcome(false, "Choose the equipment that needs maintenance.", "equipmentItemId");
        if (string.IsNullOrWhiteSpace(description))
            return Outcome(false, "Describe the problem so the repair can be tracked.", "description");
        if (description.Length > 2000)
            return Outcome(false, "Keep the description to 2000 characters or fewer.", "description");
        await using var transaction = await work.BeginAsync(cancellationToken);
        try
        {
            await locks.AcquireAsync( $"CampusGear:EquipmentItem:{equipmentItemId:N}", cancellationToken);
            var item = await equipment.FindAsync(equipmentItemId, cancellationToken);
            if (item is null || !item.IsActive) return Outcome(false, "That equipment no longer exists or is inactive. Reload and choose another item.", "equipmentItemId");
            if (await equipment.HasOutstandingLoanAsync(item.Id, cancellationToken))
                return Outcome(false, "This item is on loan. Record its return before placing it in maintenance.", "equipmentItemId");
            if (await cases.HasOpenAsync(item.Id, cancellationToken))
                return Outcome(false, "This item already has an open maintenance case. Update that case instead.", "equipmentItemId");
            var now = clock.GetUtcNow().UtcDateTime;
            var maintenance = new MaintenanceCase
            {
                EquipmentItemId = item.Id, Description = description, OpenedAtUtc = now,
                OpenedByUserId = actor.UserId!
            };
            item.IsMaintenanceHold = true;
            item.UpdatedAtUtc = now;
            cases.Add(maintenance);
            audits.Add(WorkflowRecords.Audit(actor, "maintenance.opened",
                nameof(MaintenanceCase), maintenance.Id.ToString(), now, null, JsonSerializer.Serialize(new { maintenance.EquipmentItemId, maintenance.Status })));
            await work.SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Outcome(true, $"{item.Name} ({item.AssetCode}) is now in maintenance. New reservations and releases are blocked until the case is closed.");
        }
        catch (Exception exception) when (exception is PersistenceException or InvalidOperationException)
        { return Outcome(false, exception is PersistenceException p ? p.Failure == PersistenceFailure.Busy ? "This record is busy. Please retry." : "This record changed. Reload and retry." : exception.Message); }
    }

    public async Task<OperationResult<bool>> UpdateAsync(RequestContext actor, Guid id, MaintenanceCommand input, CancellationToken cancellationToken)
    {
        if (input.Resolution?.Length > 2000 || !Enum.IsDefined(input.Status) || !Enum.IsDefined(input.Condition))
            return Outcome(false, "Choose a valid status and equipment condition, and keep the notes to 2000 characters.");
        if (input.Status == MaintenanceStatus.Closed && string.IsNullOrWhiteSpace(input.Resolution))
            return Outcome(false, "Describe the repair before closing the case.", "Resolution");
        if (input.Status == MaintenanceStatus.Closed && input.Condition is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable)
            return Outcome(false, "Choose Excellent, Good, or Fair as the condition after repair before closing the case.", "Condition");
        var itemId = await cases.GetItemIdAsync(id, cancellationToken);
        if (itemId is null) return Outcome(false, "This maintenance case no longer exists. Reload the list.", status: OperationOutcome.NotFound);
        await using var transaction = await work.BeginAsync(cancellationToken);
        try
        {
            await locks.AcquireAsync( $"CampusGear:EquipmentItem:{itemId.Value:N}", cancellationToken);
            var maintenance = await cases.FindWithItemAsync(id, cancellationToken);
            if (!WorkflowRecords.MatchesVersion(input.RowVersion, maintenance.RowVersion))
                return Outcome(false, "Someone else changed this case while you were editing. Close this dialog to see the latest details, then try again.");
            if (maintenance.Status == MaintenanceStatus.Closed) return Outcome(false, "This maintenance case is already closed.");
            var before = maintenance.Status;
            var now = clock.GetUtcNow().UtcDateTime;
            maintenance.Status = input.Status;
            maintenance.Resolution = string.IsNullOrWhiteSpace(input.Resolution) ? null : input.Resolution.Trim();
            var item = maintenance.EquipmentItem;
            if (input.Status == MaintenanceStatus.Closed)
            {
                maintenance.ClosedAtUtc = now;
                maintenance.ClosedByUserId = actor.UserId!;
                item.Condition = input.Condition;
                item.IsMaintenanceHold = await cases.HasOpenAsync(item.Id, cancellationToken, id);
            }
            item.UpdatedAtUtc = now;
            audits.Add(WorkflowRecords.Audit(actor, "maintenance.updated",
                nameof(MaintenanceCase), id.ToString(), now, JsonSerializer.Serialize(new { Status = before.ToString() }),
                JsonSerializer.Serialize(new { Status = maintenance.Status.ToString(), item.Condition, item.IsMaintenanceHold })));
            await work.SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Outcome(true, input.Status == MaintenanceStatus.Closed
                ? $"Repair recorded. {item.Name} ({item.AssetCode}) is back in service in {item.Condition} condition."
                : $"Maintenance saved for {item.Name} ({item.AssetCode}). Status: {(input.Status == MaintenanceStatus.InProgress ? "In progress" : "Open")}.");
        }
        catch (Exception exception) when (exception is PersistenceException or InvalidOperationException)
        { return Outcome(false, exception is PersistenceException p ? p.Failure == PersistenceFailure.Busy ? "This record is busy. Please retry." : "This record changed. Reload and retry." : exception.Message); }
    }
    private static OperationResult<bool> Outcome(bool ok, string message, string? field = null, OperationOutcome? status = null) =>
        new(status ?? (ok ? OperationOutcome.Success : OperationOutcome.Invalid), ok, message,
            ok ? null : [new(field ?? "", message)]);
}
