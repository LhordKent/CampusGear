using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AutoMapper;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Categories are added, edited and deleted; there is no "inactive" state for administrators to manage.
    /// Inactive equipment can never be deleted (it carries borrowing history) and still references its category,
    /// so deleting a category that only holds inactive equipment keeps the row with IsActive = false. Such a
    /// category is treated as deleted everywhere: hidden from lists and pickers, and not editable. Adding a
    /// category with its name brings it back, with its original asset code prefix.
    /// </summary>
    public sealed class CategoryService : ServiceBase, ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IAssetCodeRepository _assetCodes;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWorkflowLockManager _lockManager;
        private readonly IAuditRepository _auditRepository;
        private readonly TimeProvider _timeProvider;
        private readonly IMapper _mapper;

        public CategoryService(
            ICategoryRepository categories,
            IAssetCodeRepository assetCodes,
            IUnitOfWork work,
            IWorkflowLockManager locks,
            IAuditRepository audits,
            TimeProvider clock,
            IMapper mapper
        )
        {
            _categoryRepository = categories;
            _assetCodes = assetCodes;
            _unitOfWork = work;
            _lockManager = locks;
            _auditRepository = audits;
            _timeProvider = clock;
            _mapper = mapper;
        }

        public async Task<CategoryEditModel?> GetEditAsync(Guid id, CancellationToken ct)
        {
            var category = await _categoryRepository.FindAsync(id, ct);
            if (category is not { IsActive: true })
            {
                return null;
            }

            return new CategoryEditModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                RowVersion = Convert.ToBase64String(category.RowVersion),
            };
        }

        public async Task<CategoryPage> GetPageAsync(string? query, CancellationToken ct)
        {
            query = query?.Trim();
            if (query?.Length > 120)
            {
                query = query[..120];
            }

            var data = await _categoryRepository.ListAsync(query, ct);
            return new(_mapper.Map<List<CategoryListItem>>(data.Rows), query ?? "", data.Total);
        }

        public async Task<OperationResult<Guid>> SaveAsync(RequestContext actor, CategoryEditModel input, CancellationToken ct)
        {
            var validation = new List<ValidationResult>();
            if (
                !Validator.TryValidateObject(input, new ValidationContext(input), validation, true) || string.IsNullOrWhiteSpace(input.Name)
            )
            {
                var error = validation.FirstOrDefault();
                return OperationResult<Guid>.Invalid(
                    error?.ErrorMessage ?? Errors.NameRequired,
                    error?.MemberNames.FirstOrDefault() ?? nameof(input.Name)
                );
            }

            var name = input.Name.Trim();
            var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            await using var transaction = await _unitOfWork.BeginAsync(ct);
            try
            {
                // Two saves using the same name (or reviving the same deleted category) queue here, so the
                // name check below is not raced. Names are unique case-insensitively, so the key is too.
                await _lockManager.AcquireAsync($"CampusGear:EquipmentCategoryName:{name.ToUpperInvariant()}", ct);
                var sameName = await _categoryRepository.FindByNameAsync(name, ct);
                if (input.Id is null && sameName is { IsActive: false })
                {
                    return await RestoreAsync(actor, sameName, name, description, transaction, ct);
                }

                if (sameName is not null && sameName.Id != input.Id)
                {
                    return OperationResult<Guid>.Invalid(
                        sameName.IsActive ? Errors.CategoryNameTaken : Errors.CategoryNameDeleted,
                        nameof(input.Name)
                    );
                }

                var id = input.Id ?? Guid.NewGuid();
                // Inventory saves targeting this category take the same lock before checking that it still exists.
                await _lockManager.AcquireAsync($"CampusGear:EquipmentCategory:{id:N}", ct);
                var category = input.Id is null ? new EquipmentCategory { Id = id } : await _categoryRepository.FindAsync(id, ct);
                if (category is not { IsActive: true })
                {
                    return OperationResult<Guid>.Missing();
                }

                if (input.Id is not null && !WorkflowRecords.MatchesVersion(input.RowVersion, category.RowVersion))
                {
                    return OperationResult<Guid>.Invalid(Errors.StaleCategory);
                }

                var before = input.Id is null ? null : Snapshot(category);
                category.Name = name;
                category.Description = description;
                category.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                if (input.Id is null)
                {
                    _categoryRepository.Add(category);
                    await _assetCodes.CreateSeriesAsync(category, ct);
                }

                AddAudit(actor, input.Id is null ? "category.created" : "category.updated", category, before);
                await _unitOfWork.SaveAsync(ct);
                await transaction.CommitAsync(ct);
                return OperationResult<Guid>.Ok(id, Common.CategorySaved);
            }
            catch (PersistenceException exception)
                when (exception.Failure is PersistenceFailure.StaleWrite or PersistenceFailure.Duplicate or PersistenceFailure.Busy)
            {
                await transaction.RollbackAsync(ct);
                _unitOfWork.ClearTracking();
                return OperationResult<Guid>.Invalid(
                    exception.Failure == PersistenceFailure.Busy ? Errors.RecordBusy : Errors.DuplicateOrStaleCategory
                );
            }
        }

        public async Task<OperationResult<Guid>> DeleteAsync(RequestContext actor, Guid id, CancellationToken ct)
        {
            await using var transaction = await _unitOfWork.BeginAsync(ct);
            try
            {
                await _lockManager.AcquireAsync($"CampusGear:EquipmentCategory:{id:N}", ct);
                var category = await _categoryRepository.FindAsync(id, ct);
                if (category is not { IsActive: true })
                {
                    return OperationResult<Guid>.Missing();
                }

                if (await _categoryRepository.HasItemsAsync(id, true, ct))
                {
                    return OperationResult<Guid>.Invalid(string.Format(Errors.CategoryHasActiveEquipment, category.Name));
                }

                // Inactive equipment still points at this row, so it stays (hidden) instead of being removed.
                var keepRow = await _categoryRepository.HasItemsAsync(id, false, ct);
                var before = Snapshot(category);
                if (keepRow)
                {
                    category.IsActive = false;
                    category.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                }
                else
                {
                    _categoryRepository.Remove(category);
                }

                _auditRepository.Add(
                    WorkflowRecords.Audit(
                        actor,
                        "category.deleted",
                        nameof(EquipmentCategory),
                        id.ToString(),
                        _timeProvider.GetUtcNow().UtcDateTime,
                        before,
                        null
                    )
                );
                await _unitOfWork.SaveAsync(ct);
                await transaction.CommitAsync(ct);
                return OperationResult<Guid>.Ok(id, $"{category.Name} was deleted.");
            }
            catch (PersistenceException exception)
                when (exception.Failure is PersistenceFailure.StaleWrite or PersistenceFailure.Busy)
            {
                await transaction.RollbackAsync(ct);
                _unitOfWork.ClearTracking();
                return OperationResult<Guid>.Invalid(
                    exception.Failure == PersistenceFailure.Busy ? Errors.RecordBusy : Errors.StaleCategory
                );
            }
        }

        public async Task<OperationResult<Guid>> AddFromInventoryAsync(RequestContext actor, string? name, CancellationToken ct)
        {
            name = name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            {
                return OperationResult<Guid>.Invalid(Errors.InvalidNameLength);
            }

            var result = await SaveAsync(actor, new CategoryEditModel { Name = name }, ct);
            // A restored category keeps its own message, which says so.
            return result.Succeeded && result.Message == Common.CategorySaved ? result with { Message = Common.CategoryAdded } : result;
        }

        /// <summary>Brings a deleted category back under the same row, so its asset code prefix and inactive equipment return with it.</summary>
        private async Task<OperationResult<Guid>> RestoreAsync(
            RequestContext actor,
            EquipmentCategory category,
            string name,
            string? description,
            IDataTransaction transaction,
            CancellationToken ct
        )
        {
            await _lockManager.AcquireAsync($"CampusGear:EquipmentCategory:{category.Id:N}", ct);
            var before = Snapshot(category);
            category.IsActive = true;
            category.Name = name;
            category.Description = description ?? category.Description;
            category.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            AddAudit(actor, "category.restored", category, before);
            // The row was read before its lock; its rowversion makes a concurrent change fail as a stale write.
            await _unitOfWork.SaveAsync(ct);
            await transaction.CommitAsync(ct);
            return OperationResult<Guid>.Ok(category.Id, string.Format(Common.CategoryRestored, category.Name));
        }

        private void AddAudit(RequestContext actor, string action, EquipmentCategory category, string? before)
        {
            _auditRepository.Add(
                WorkflowRecords.Audit(
                    actor,
                    action,
                    nameof(EquipmentCategory),
                    category.Id.ToString(),
                    _timeProvider.GetUtcNow().UtcDateTime,
                    before,
                    Snapshot(category)
                )
            );
        }

        private static string Snapshot(EquipmentCategory category)
        {
            return JsonSerializer.Serialize(new { category.Name, category.Description });
        }
    }
}
