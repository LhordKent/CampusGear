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
    public sealed class CategoryService : ServiceBase, ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWorkflowLockManager _lockManager;
        private readonly IAuditRepository _auditRepository;
        private readonly TimeProvider _timeProvider;
        private readonly IMapper _mapper;

        public CategoryService(
            ICategoryRepository categories,
            IUnitOfWork work,
            IWorkflowLockManager locks,
            IAuditRepository audits,
            TimeProvider clock,
            IMapper mapper
        )
        {
            _categoryRepository = categories;
            _unitOfWork = work;
            _lockManager = locks;
            _auditRepository = audits;
            _timeProvider = clock;
            _mapper = mapper;
        }

        public async Task<CategoryEditModel?> GetEditAsync(Guid id, CancellationToken ct)
        {
            var category = await _categoryRepository.FindAsync(id, ct);
            if (category is null)
            {
                return null;
            }

            return new CategoryEditModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                IsActive = category.IsActive,
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

            var id = input.Id ?? Guid.NewGuid();
            // Inventory saves targeting this category take the same lock before checking its active status.
            await using var transaction = await _unitOfWork.BeginAsync(ct);
            try
            {
                await _lockManager.AcquireAsync($"CampusGear:EquipmentCategory:{id:N}", ct);
                var category = input.Id is null ? new EquipmentCategory { Id = id } : await _categoryRepository.FindAsync(id, ct);
                if (category is null)
                {
                    return OperationResult<Guid>.Missing();
                }

                if (input.Id is not null && !WorkflowRecords.MatchesVersion(input.RowVersion, category.RowVersion))
                {
                    return OperationResult<Guid>.Invalid(Errors.StaleCategory);
                }

                if (!input.IsActive && await _categoryRepository.HasItemsAsync(id, true, ct))
                {
                    return OperationResult<Guid>.Invalid(Errors.ActiveEquipmentPreventsDeactivation);
                }

                var before = input.Id is null
                    ? null
                    : JsonSerializer.Serialize(
                        new
                        {
                            category.Name,
                            category.Description,
                            category.IsActive,
                        }
                    );
                category.Name = input.Name.Trim();
                category.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
                category.IsActive = input.IsActive;
                category.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                if (input.Id is null)
                {
                    _categoryRepository.Add(category);
                }

                _auditRepository.Add(
                    WorkflowRecords.Audit(
                        actor,
                        input.Id is null ? "category.created" : "category.updated",
                        nameof(EquipmentCategory),
                        id.ToString(),
                        _timeProvider.GetUtcNow().UtcDateTime,
                        before,
                        JsonSerializer.Serialize(
                            new
                            {
                                category.Name,
                                category.Description,
                                category.IsActive,
                            }
                        )
                    )
                );
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
            var category = await _categoryRepository.FindAsync(id, ct);
            if (category is null)
            {
                return OperationResult<Guid>.Missing();
            }

            var inUse = await _categoryRepository.HasItemsAsync(id, false, ct);
            var before = JsonSerializer.Serialize(
                new
                {
                    category.Name,
                    category.Description,
                    category.IsActive,
                }
            );
            if (inUse)
            {
                category.IsActive = false;
            }
            else
            {
                _categoryRepository.Remove(category);
            }

            _auditRepository.Add(
                WorkflowRecords.Audit(
                    actor,
                    inUse ? "category.deactivated" : "category.deleted",
                    nameof(EquipmentCategory),
                    id.ToString(),
                    _timeProvider.GetUtcNow().UtcDateTime,
                    before,
                    null
                )
            );
            await _unitOfWork.SaveAsync(ct);
            return OperationResult<Guid>.Ok(
                id,
                inUse
                    ? $"{category.Name} still holds equipment, so it was deactivated instead of deleted."
                    : $"{category.Name} was deleted."
            );
        }

        public async Task<OperationResult<Guid>> AddFromInventoryAsync(RequestContext actor, string? name, CancellationToken ct)
        {
            name = name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            {
                return OperationResult<Guid>.Invalid(Errors.InvalidNameLength);
            }

            var category = new EquipmentCategory { Name = name };
            _categoryRepository.Add(category);
            _auditRepository.Add(
                WorkflowRecords.Audit(
                    actor,
                    "category.created",
                    nameof(EquipmentCategory),
                    category.Id.ToString(),
                    _timeProvider.GetUtcNow().UtcDateTime,
                    null,
                    JsonSerializer.Serialize(new { name })
                )
            );
            try
            {
                await _unitOfWork.SaveAsync(ct);
                return OperationResult<Guid>.Ok(category.Id, Common.CategoryAdded);
            }
            catch (PersistenceException e) when (e.Failure == PersistenceFailure.Duplicate)
            {
                return OperationResult<Guid>.Invalid(Errors.DuplicateCategory);
            }
        }
    }
}
