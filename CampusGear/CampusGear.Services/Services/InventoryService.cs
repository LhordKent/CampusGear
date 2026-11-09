using System.Text.Json;
using CampusGear.Data.Interfaces;
using CampusGear.Data.QueryModels;
using CampusGear.Models;
using CampusGear.Services.ServiceModels;
namespace CampusGear.Services;

public sealed class InventoryService(IEquipmentRepository equipment, ICategoryRepository categories,
    IMaintenanceRepository maintenance, IAuditRepository audits, IUnitOfWork work,
    IWorkflowLockManager locks, TimeProvider clock, IEquipmentImageStore images) : IInventoryService
{
    public async Task<EquipmentRecord?> GetEditAsync(Guid id, CancellationToken ct)
    {
        var item = await equipment.FindAsync(id, ct);
        return item is null ? null : EquipmentRecord.From(item);
    }
    public async Task<InventoryPage> GetPageAsync(string? query, int page, CancellationToken ct)
    {
        query = query?.Trim();
        return new(await equipment.InventoryAsync(query, page, ct), query ?? "");
    }
    public async Task<OperationResult<Guid>> DeleteAsync(RequestContext actor, Guid id, CancellationToken ct)
    {
        var item = await equipment.FindAsync(id, ct);
        if (item is null) return OperationResult<Guid>.Missing();
        var hasHistory = await equipment.HasHistoryAsync(id, ct);
        var before = JsonSerializer.Serialize(new { item.AssetCode, item.Name, item.IsActive, item.Condition });
        var photo = item.ImagePath;
        if (hasHistory) item.IsActive = false; else equipment.Remove(item);
        audits.Add(WorkflowRecords.Audit(actor, hasHistory ? "equipment.deactivated" : "equipment.deleted",
            nameof(EquipmentItem), id.ToString(), clock.GetUtcNow().UtcDateTime, before, null));
        await work.SaveAsync(ct);
        if (!hasHistory) images.Delete(photo);
        return OperationResult<Guid>.Ok(id, hasHistory
            ? $"{item.AssetCode} has borrowing or repair history, so it was deactivated instead of deleted."
            : $"{item.AssetCode} was deleted.");
    }
    public async Task<OperationResult<Guid>> SaveAsync(RequestContext actor, EquipmentCommand input,
        UploadContent? imageFile, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        input.AssetCode = input.AssetCode?.Trim() ?? "";
        input.Name = input.Name?.Trim() ?? "";
        if (!Enum.IsDefined(input.Condition)) errors.Add(new("Input.Condition", "Choose a valid condition."));
        if (!await categories.IsActiveAsync(input.CategoryId, cancellationToken))
            errors.Add(new("Input.CategoryId", "Choose an active category."));
        if (imageFile is { Length: > 0 } && imageFile.Length > images.MaxBytes)
            errors.Add(new("imageFile", "Use an image of 5 MB or smaller."));
        if (errors.Count > 0) return new(OperationOutcome.Invalid, Errors: errors);

        var itemId = input.Id ?? Guid.NewGuid();
        // The new photo is written first so its path can be saved with the record; it is removed again if the save fails.
        string? newImagePath = null;
        if (imageFile is { Length: > 0 })
        {
            try
            {
                var stream = imageFile.Content;
                newImagePath = await images.SaveAsync(itemId, stream, imageFile.Length, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                return OperationResult<Guid>.Invalid(exception.Message, "imageFile");
            }
        }
        string? replacedImagePath = null;
        await using var transaction = await work.BeginAsync(cancellationToken);
        try
        {
            await locks.AcquireAsync($"CampusGear:EquipmentItem:{itemId:N}", cancellationToken);
            await locks.AcquireAsync($"CampusGear:EquipmentCategory:{input.CategoryId:N}", cancellationToken);
            if (!await categories.IsActiveAsync(input.CategoryId, cancellationToken))
                throw new InvalidOperationException("This category is inactive. Choose another category.");
            var item = input.Id is null ? new EquipmentItem { Id = itemId } :
                await equipment.FindAsync(itemId, cancellationToken);
            if (item is null) { images.Delete(newImagePath); return OperationResult<Guid>.Missing(); }
            if (input.Id is not null && !WorkflowRecords.MatchesVersion(input.RowVersion, item.RowVersion))
                throw new InvalidOperationException("This equipment changed. Reload it before saving.");
            if (await equipment.HasOutstandingLoanAsync(itemId, cancellationToken))
                throw new InvalidOperationException("Record the active loan's return before editing this equipment.");
            var now = clock.GetUtcNow().UtcDateTime;
            if (!input.IsActive && await equipment.HasLiveReservationsAsync(itemId, now, cancellationToken))
                throw new InvalidOperationException("Resolve this item's pending and approved reservations before deactivating it.");

            var before = input.Id is null ? null : JsonSerializer.Serialize(new { item.AssetCode, item.Name, item.Condition, item.IsActive });
            item.CategoryId = input.CategoryId;
            item.AssetCode = input.AssetCode;
            item.Name = input.Name;
            item.SerialNumber = Clean(input.SerialNumber);
            item.Location = Clean(input.Location);
            item.Description = Clean(input.Description);
            item.Condition = input.Condition;
            item.IsActive = input.IsActive;
            if (newImagePath is not null || input.RemoveImage)
            {
                replacedImagePath = item.ImagePath;
                item.ImagePath = newImagePath;
            }
            item.UpdatedAtUtc = now;
            if (input.Id is null) equipment.Add(item);
            if (input.Condition is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable)
            {
                item.IsMaintenanceHold = true;
                if (!await maintenance.HasOpenAsync(itemId, cancellationToken))
                    maintenance.Add(new MaintenanceCase
                    {
                        EquipmentItemId = itemId,
                        Description = $"Inventory condition recorded as {input.Condition}.",
                        OpenedByUserId = actor.UserId!,
                        OpenedAtUtc = now
                    });
            }
            // Open maintenance can only be cleared by recording its resolution.
            if (await maintenance.HasOpenAsync(itemId, cancellationToken))
                item.IsMaintenanceHold = true;
            audits.Add(WorkflowRecords.Audit(actor,
                input.Id is null ? "equipment.created" : "equipment.updated", nameof(EquipmentItem), itemId.ToString(), now,
                before, JsonSerializer.Serialize(new { item.AssetCode, item.Name, item.Condition, item.IsActive, item.IsMaintenanceHold })));
            await work.SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            images.Delete(replacedImagePath);
            return OperationResult<Guid>.Ok(itemId, input.Id is null ? "Equipment added." : "Equipment updated.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or
            PersistenceException { Failure: PersistenceFailure.StaleWrite or PersistenceFailure.Duplicate or PersistenceFailure.Busy })
        {
            await transaction.RollbackAsync(cancellationToken);
            work.ClearTracking();
            images.Delete(newImagePath);
            return OperationResult<Guid>.Invalid(exception is PersistenceException p
                ? p.Failure == PersistenceFailure.Busy ? "This record is busy. Please retry."
                    : "The asset code or serial number already exists, or the record changed. Reload and try again."
                : exception.Message);
        }
    }
    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
