using System.Text.Json;
using CampusGear.Data.Interfaces;
using CampusGear.Models;
using CampusGear.Services.ServiceModels;
namespace CampusGear.Services;

public sealed class CategoryService(ICategoryRepository categories, IUnitOfWork work,
    IWorkflowLockManager locks, IAuditRepository audits, TimeProvider clock) : ICategoryService
{
    public async Task<CategoryCommand?> GetEditAsync(Guid id, CancellationToken ct)
    {
        var x = await categories.FindAsync(id, ct);
        return x is null ? null : new(x.Id, x.Name, x.Description, x.IsActive, Convert.ToBase64String(x.RowVersion));
    }
    public async Task<CategoryPage> GetPageAsync(string? query, CancellationToken ct)
    {
        query = query?.Trim();
        if (query?.Length > 120) query = query[..120];
        var data = await categories.ListAsync(query, ct);
        return new(data.Rows, query ?? "", data.Total);
    }
    public async Task<OperationResult<Guid>> SaveAsync(RequestContext actor, CategoryCommand input, CancellationToken ct)
    {
        var id = input.Id ?? Guid.NewGuid();
        await using var transaction = await work.BeginAsync(ct);
        try
        {
            await locks.AcquireAsync($"CampusGear:EquipmentCategory:{id:N}", ct);
            var category = input.Id is null ? new EquipmentCategory { Id = id } : await categories.FindAsync(id, ct);
            if (category is null) return OperationResult<Guid>.Missing();
            if (input.Id is not null && !WorkflowRecords.MatchesVersion(input.RowVersion, category.RowVersion))
                return OperationResult<Guid>.Invalid("This category changed. Reload before saving.");
            if (!input.IsActive && await categories.HasItemsAsync(id, true, ct))
                return OperationResult<Guid>.Invalid("Move or deactivate this category's active equipment before deactivating the category.");
            var before = input.Id is null ? null : JsonSerializer.Serialize(new { category.Name, category.Description, category.IsActive });
            category.Name = input.Name.Trim();
            category.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            category.IsActive = input.IsActive;
            category.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            if (input.Id is null) categories.Add(category);
            audits.Add(WorkflowRecords.Audit(actor, input.Id is null ? "category.created" : "category.updated",
                nameof(EquipmentCategory), id.ToString(), clock.GetUtcNow().UtcDateTime, before,
                JsonSerializer.Serialize(new { category.Name, category.Description, category.IsActive })));
            await work.SaveAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<Guid>.Ok(id, "Category saved.");
        }
        catch (PersistenceException exception) when (exception.Failure is PersistenceFailure.StaleWrite or PersistenceFailure.Duplicate or PersistenceFailure.Busy)
        {
            await transaction.RollbackAsync(ct);
            work.ClearTracking();
            return OperationResult<Guid>.Invalid(exception.Failure == PersistenceFailure.Busy
                ? "This record is busy. Please retry." : "That category name already exists, or the record changed.");
        }
    }
    public async Task<OperationResult<Guid>> DeleteAsync(RequestContext actor, Guid id, CancellationToken ct)
    {
        var category = await categories.FindAsync(id, ct);
        if (category is null) return OperationResult<Guid>.Missing();
        var inUse = await categories.HasItemsAsync(id, false, ct);
        var before = JsonSerializer.Serialize(new { category.Name, category.Description, category.IsActive });
        if (inUse) category.IsActive = false; else categories.Remove(category);
        audits.Add(WorkflowRecords.Audit(actor, inUse ? "category.deactivated" : "category.deleted",
            nameof(EquipmentCategory), id.ToString(), clock.GetUtcNow().UtcDateTime, before, null));
        await work.SaveAsync(ct);
        return OperationResult<Guid>.Ok(id, inUse
            ? $"{category.Name} still holds equipment, so it was deactivated instead of deleted."
            : $"{category.Name} was deleted.");
    }
    public async Task<OperationResult<Guid>> AddFromInventoryAsync(RequestContext actor, string? name, CancellationToken ct)
    {
        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            return OperationResult<Guid>.Invalid("Enter a category name using 120 characters or fewer.");
        var category = new EquipmentCategory { Name = name };
        categories.Add(category);
        audits.Add(WorkflowRecords.Audit(actor, "category.created", nameof(EquipmentCategory), category.Id.ToString(),
            clock.GetUtcNow().UtcDateTime, null, JsonSerializer.Serialize(new { name })));
        try { await work.SaveAsync(ct); return OperationResult<Guid>.Ok(category.Id, "Category added."); }
        catch (PersistenceException e) when (e.Failure == PersistenceFailure.Duplicate)
        { return OperationResult<Guid>.Invalid("That category already exists."); }
    }
}
