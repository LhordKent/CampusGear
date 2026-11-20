using System.Globalization;
using System.Text.Json;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

// =====================================================================
// INVENTORY SERVICE  -  add, edit and delete equipment items (admin Equipment page).
// FLOW: AdminInventoryController -> this service -> EquipmentRepository, AssetCodeRepository,
//       MaintenanceRepository -> database. Photos are saved to disk by EquipmentImageStore.
// =====================================================================
namespace ASI.Basecode.Services.Services
{
    public sealed class InventoryService : ServiceBase, IInventoryService
    {
        private readonly IEquipmentRepository _equipmentRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IAssetCodeRepository _assetCodes;
        private readonly IMaintenanceRepository _maintenanceRepository;
        private readonly IAuditRepository _auditRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWorkflowLockManager _lockManager;
        private readonly TimeProvider _timeProvider;
        private readonly IEquipmentImageStore _images;

        public InventoryService(
            IEquipmentRepository equipment,
            ICategoryRepository categories,
            IAssetCodeRepository assetCodes,
            IMaintenanceRepository maintenance,
            IAuditRepository audits,
            IUnitOfWork work,
            IWorkflowLockManager locks,
            TimeProvider clock,
            IEquipmentImageStore images
        )
        {
            _equipmentRepository = equipment;
            _categoryRepository = categories;
            _assetCodes = assetCodes;
            _maintenanceRepository = maintenance;
            _auditRepository = audits;
            _unitOfWork = work;
            _lockManager = locks;
            _timeProvider = clock;
            _images = images;
        }

        // Loads one item for the Edit modal.
        public async Task<EquipmentRecord?> GetEditAsync(Guid id, CancellationToken ct)
        {
            var item = await _equipmentRepository.FindAsync(id, ct);
            return item is null ? null : EquipmentRecord.From(item);
        }

        // What blocks editing right now: an open loan (no edits) or upcoming bookings (must stay active).
        public Task<EquipmentEditState> GetEditStateAsync(Guid id, CancellationToken ct)
        {
            return _equipmentRepository.EditStateAsync(id, _timeProvider.GetUtcNow().UtcDateTime, ct);
        }

        // The equipment list for the page (15 per page), plus the categories for the dropdown.
        public async Task<InventoryPage> GetPageAsync(string? query, int page, CancellationToken ct)
        {
            query = query?.Trim();
            return new(await _equipmentRepository.InventoryAsync(query, page, ct), query ?? "");
        }

        // DELETE: an item with borrowing/repair history is only made inactive (history is kept);
        // an item with no history is removed completely, together with its photo.
        public async Task<OperationResult<Guid>> DeleteAsync(RequestContext actor, Guid id, CancellationToken ct)
        {
            var item = await _equipmentRepository.FindAsync(id, ct);
            if (item is null)
            {
                return OperationResult<Guid>.Missing();
            }

            var hasHistory = await _equipmentRepository.HasHistoryAsync(id, ct);
            var before = JsonSerializer.Serialize(
                new
                {
                    item.AssetCode,
                    item.Name,
                    item.IsActive,
                    item.Condition,
                }
            );
            var photo = item.ImagePath;
            if (hasHistory)
            {
                item.IsActive = false;
            }
            else
            {
                _equipmentRepository.Remove(item);
            }

            _auditRepository.Add(
                WorkflowRecords.Audit(
                    actor,
                    hasHistory ? "equipment.deactivated" : "equipment.deleted",
                    nameof(EquipmentItem),
                    id.ToString(),
                    _timeProvider.GetUtcNow().UtcDateTime,
                    before,
                    null
                )
            );
            await _unitOfWork.SaveAsync(ct);
            if (!hasHistory)
            {
                _images.Delete(photo);
            }

            return OperationResult<Guid>.Ok(
                id,
                hasHistory
                    ? $"{item.AssetCode} has borrowing or repair history, so it was deactivated instead of deleted."
                    : $"{item.AssetCode} was deleted."
            );
        }

        // SAVE (add or edit). Steps:
        //   1. quick checks (condition, category, photo size)
        //   2. save the new photo file first
        //   3. lock the item + category, re-check, and refuse if the item is on loan
        //   4. copy the form values onto the item; a NEW item gets its asset code (e.g. CAM-004)
        //   5. Damaged -> maintenance hold + open repair case
        //   6. audit log, save, commit. If anything fails, the new photo file is deleted again.
        public async Task<OperationResult<Guid>> SaveAsync(
            RequestContext actor,
            EquipmentEditModel input,
            UploadContent? imageFile,
            CancellationToken cancellationToken
        )
        {
            var errors = new List<FieldError>();
            // Step 1: quick checks before touching the database.
            input.Name = input.Name?.Trim() ?? "";
            if (!Enum.IsDefined(input.Condition))
            {
                errors.Add(new("Input.Condition", Errors.InvalidCondition));
            }

            if (!await _categoryRepository.IsActiveAsync(input.CategoryId, cancellationToken))
            {
                errors.Add(new("Input.CategoryId", Errors.ActiveCategoryRequired));
            }

            if (imageFile is { Length: > 0 } && imageFile.Length > _images.MaxBytes)
            {
                errors.Add(new("imageFile", Errors.ImageTooLarge));
            }

            if (errors.Count > 0)
            {
                return new(OperationOutcome.Invalid, Errors: errors);
            }

            var itemId = input.Id ?? Guid.NewGuid();
            // The new photo is written first so its path can be saved with the record; it is removed again if the save fails.
            string? newImagePath = null;
            if (imageFile is { Length: > 0 })
            {
                try
                {
                    var stream = imageFile.Content;
                    newImagePath = await _images.SaveAsync(itemId, stream, imageFile.Length, cancellationToken);
                }
                catch (InvalidOperationException exception)
                {
                    return OperationResult<Guid>.Invalid(exception.Message, "imageFile");
                }
            }

            string? replacedImagePath = null;
            string? assignedCode = null;
            IDataTransaction? transaction = null;
            var committed = false;
            try
            {
                // Include transaction creation in the compensation scope: the photo
                // already exists even if the database cannot begin a transaction.
                // Step 3: lock the item and its category so nobody changes them at the same time.
                transaction = await _unitOfWork.BeginAsync(cancellationToken);
                await _lockManager.AcquireAsync($"CampusGear:EquipmentItem:{itemId:N}", cancellationToken);
                await _lockManager.AcquireAsync($"CampusGear:EquipmentCategory:{input.CategoryId:N}", cancellationToken);
                if (!await _categoryRepository.IsActiveAsync(input.CategoryId, cancellationToken))
                {
                    return OperationResult<Guid>.Invalid(Errors.InactiveCategory);
                }

                var item = input.Id is null
                    ? new EquipmentItem { Id = itemId }
                    : await _equipmentRepository.FindAsync(itemId, cancellationToken);
                if (item is null)
                {
                    return OperationResult<Guid>.Missing();
                }

                if (input.Id is not null && !WorkflowRecords.MatchesVersion(input.RowVersion, item.RowVersion))
                {
                    return OperationResult<Guid>.Invalid(Errors.StaleEquipment);
                }

                // An item that is out on loan can't be edited until it is returned.
                if (await _equipmentRepository.HasOutstandingLoanAsync(itemId, cancellationToken))
                {
                    return OperationResult<Guid>.Invalid(Errors.ActiveLoanPreventsEdit);
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                // Can't make it inactive while students still have upcoming bookings for it.
                if (!input.IsActive && await _equipmentRepository.HasLiveReservationsAsync(itemId, now, cancellationToken))
                {
                    return OperationResult<Guid>.Invalid(Errors.LiveReservationsPreventDeactivation);
                }

                var before = input.Id is null
                    ? null
                    : JsonSerializer.Serialize(
                        new
                        {
                            item.AssetCode,
                            item.Name,
                            item.Condition,
                            item.IsActive,
                        }
                    );
                // Step 4: copy the form values onto the item.
                item.CategoryId = input.CategoryId;
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
                // New item: take the next asset code from the category's series (CAM-001, CAM-002, ...).
                if (input.Id is null)
                {
                    assignedCode = await _assetCodes.AllocateAsync(input.CategoryId, cancellationToken);
                    item.AssetCode = assignedCode;
                    _equipmentRepository.Add(item);
                }

                // Step 5: Damaged -> hide it from borrowers and open a repair case (if none is open yet).
                if (input.Condition is EquipmentCondition.Damaged)
                {
                    item.IsMaintenanceHold = true;
                    if (!await _maintenanceRepository.HasOpenAsync(itemId, cancellationToken))
                    {
                        _maintenanceRepository.Add(
                            new MaintenanceCase
                            {
                                EquipmentItemId = itemId,
                                Description = $"Inventory condition recorded as {input.Condition}.",
                                OpenedByUserId = actor.UserId!,
                                OpenedAtUtc = now,
                            }
                        );
                    }
                }

                // Open maintenance can only be cleared by recording its resolution.
                if (await _maintenanceRepository.HasOpenAsync(itemId, cancellationToken))
                {
                    item.IsMaintenanceHold = true;
                }

                // Step 6: audit log, then save and commit.
                _auditRepository.Add(
                    WorkflowRecords.Audit(
                        actor,
                        input.Id is null ? "equipment.created" : "equipment.updated",
                        nameof(EquipmentItem),
                        itemId.ToString(),
                        now,
                        before,
                        JsonSerializer.Serialize(
                            new
                            {
                                item.AssetCode,
                                item.Name,
                                item.Condition,
                                item.IsActive,
                                item.IsMaintenanceHold,
                            }
                        )
                    )
                );
                await _unitOfWork.SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                committed = true;
            }
            catch (PersistenceException exception)
                when (exception.Failure is PersistenceFailure.StaleWrite or PersistenceFailure.Duplicate or PersistenceFailure.Busy)
            {
                return OperationResult<Guid>.Invalid(
                    exception.Failure == PersistenceFailure.Busy
                        ? Errors.RecordBusy
                        : Errors.DuplicateOrStaleEquipment
                );
            }
            finally
            {
                try
                {
                    // Disposal rolls back every uncommitted path, including unexpected
                    // faults and invalid edits. Do not use an aborted HTTP token here.
                    if (transaction is not null)
                    {
                        await transaction.DisposeAsync();
                    }
                }
                finally
                {
                    if (!committed)
                    {
                        _unitOfWork.ClearTracking();
                        _images.Delete(newImagePath);
                    }
                }
            }

            _images.Delete(replacedImagePath);
            return OperationResult<Guid>.Ok(
                itemId,
                assignedCode is null
                    ? Common.EquipmentUpdated
                    : string.Format(CultureInfo.CurrentCulture, Common.EquipmentAdded, assignedCode)
            );
        }

        // Turns empty text into null and trims spaces.
        private static string? Clean(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
