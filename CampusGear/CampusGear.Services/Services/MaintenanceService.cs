using System.Text.Json;
using CampusGear.Data.Interfaces;
using CampusGear.Data.Models;
using CampusGear.Resources.Constants;
using CampusGear.Resources.Messages;
using CampusGear.Services.Helpers;
using CampusGear.Services.Interfaces;
using CampusGear.Services.ServiceModels;

namespace CampusGear.Services.Services
{
    public sealed class MaintenanceService : IMaintenanceService
    {
        private readonly IMaintenanceRepository _cases;
        private readonly IEquipmentRepository _equipmentRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWorkflowLockManager _lockManager;
        private readonly IAuditRepository _auditRepository;
        private readonly TimeProvider _timeProvider;

        public MaintenanceService(
            IMaintenanceRepository cases,
            IEquipmentRepository equipment,
            IUnitOfWork work,
            IWorkflowLockManager locks,
            IAuditRepository audits,
            TimeProvider clock
        )
        {
            _cases = cases;
            _equipmentRepository = equipment;
            _unitOfWork = work;
            _lockManager = locks;
            _auditRepository = audits;
            _timeProvider = clock;
        }

        public async Task<MaintenancePage> GetPageAsync(string? status, string? query, int page, CancellationToken ct)
        {
            status = status == "closed" ? "closed" : "open";
            query = query?.Trim();
            if (query?.Length > 120)
            {
                query = query[..120];
            }

            return new(await _cases.ListAsync(status, query, page, ct), status, query ?? "");
        }

        public async Task<OperationResult<bool>> OpenAsync(
            RequestContext actor,
            Guid equipmentItemId,
            string? description,
            CancellationToken cancellationToken
        )
        {
            description = description?.Trim();
            if (equipmentItemId == Guid.Empty)
            {
                return Outcome(false, MaintenanceMessages.EquipmentRequired, "equipmentItemId");
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return Outcome(false, MaintenanceMessages.DescriptionRequired, "description");
            }

            if (description.Length > 2000)
            {
                return Outcome(false, MaintenanceMessages.DescriptionTooLong, "description");
            }

            await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
            try
            {
                await _lockManager.AcquireAsync($"CampusGear:EquipmentItem:{equipmentItemId:N}", cancellationToken);
                var item = await _equipmentRepository.FindAsync(equipmentItemId, cancellationToken);
                if (item is null || !item.IsActive)
                {
                    return Outcome(false, MaintenanceMessages.EquipmentUnavailable, "equipmentItemId");
                }

                if (await _equipmentRepository.HasOutstandingLoanAsync(item.Id, cancellationToken))
                {
                    return Outcome(false, MaintenanceMessages.ActiveLoan, "equipmentItemId");
                }

                if (await _cases.HasOpenAsync(item.Id, cancellationToken))
                {
                    return Outcome(false, MaintenanceMessages.OpenCaseExists, "equipmentItemId");
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var maintenance = new MaintenanceCase
                {
                    EquipmentItemId = item.Id,
                    Description = description,
                    OpenedAtUtc = now,
                    OpenedByUserId = actor.UserId!,
                };
                item.IsMaintenanceHold = true;
                item.UpdatedAtUtc = now;
                _cases.Add(maintenance);
                _auditRepository.Add(
                    WorkflowRecords.Audit(
                        actor,
                        "maintenance.opened",
                        nameof(MaintenanceCase),
                        maintenance.Id.ToString(),
                        now,
                        null,
                        JsonSerializer.Serialize(new { maintenance.EquipmentItemId, maintenance.Status })
                    )
                );
                await _unitOfWork.SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Outcome(
                    true,
                    $"{item.Name} ({item.AssetCode}) is now in maintenance. New reservations and releases are blocked until the case is closed."
                );
            }
            catch (Exception exception)
                when (exception
                        is InvalidOperationException
                            or PersistenceException
                            {
                                Failure: PersistenceFailure.StaleWrite or PersistenceFailure.Duplicate or PersistenceFailure.Busy
                            }
                )
            {
                return Outcome(false, GetSaveError(exception));
            }
        }

        public async Task<OperationResult<bool>> UpdateAsync(
            RequestContext actor,
            Guid id,
            MaintenanceEditModel input,
            CancellationToken cancellationToken
        )
        {
            if (input.Resolution?.Length > 2000 || !Enum.IsDefined(input.Status) || !Enum.IsDefined(input.Condition))
            {
                return Outcome(false, MaintenanceMessages.InvalidEdit);
            }

            if (input.Status == MaintenanceStatus.Closed && string.IsNullOrWhiteSpace(input.Resolution))
            {
                return Outcome(false, MaintenanceMessages.RepairDescriptionRequired, "Resolution");
            }

            if (
                input.Status == MaintenanceStatus.Closed
                && input.Condition is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable
            )
            {
                return Outcome(false, MaintenanceMessages.InvalidClosingCondition, "Condition");
            }

            var itemId = await _cases.GetItemIdAsync(id, cancellationToken);
            if (itemId is null)
            {
                return Outcome(false, MaintenanceMessages.CaseNotFound, status: OperationOutcome.NotFound);
            }

            await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
            try
            {
                await _lockManager.AcquireAsync($"CampusGear:EquipmentItem:{itemId.Value:N}", cancellationToken);
                var maintenance = await _cases.FindWithItemAsync(id, cancellationToken);
                if (!WorkflowRecords.MatchesVersion(input.RowVersion, maintenance.RowVersion))
                {
                    return Outcome(false, MaintenanceMessages.StaleCase);
                }

                if (maintenance.Status == MaintenanceStatus.Closed)
                {
                    return Outcome(false, MaintenanceMessages.CaseAlreadyClosed);
                }

                var before = maintenance.Status;
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                maintenance.Status = input.Status;
                maintenance.Resolution = string.IsNullOrWhiteSpace(input.Resolution) ? null : input.Resolution.Trim();
                var item = maintenance.EquipmentItem;
                if (input.Status == MaintenanceStatus.Closed)
                {
                    maintenance.ClosedAtUtc = now;
                    maintenance.ClosedByUserId = actor.UserId!;
                    item.Condition = input.Condition;
                    item.IsMaintenanceHold = await _cases.HasOpenAsync(item.Id, cancellationToken, id);
                }

                item.UpdatedAtUtc = now;
                _auditRepository.Add(
                    WorkflowRecords.Audit(
                        actor,
                        "maintenance.updated",
                        nameof(MaintenanceCase),
                        id.ToString(),
                        now,
                        JsonSerializer.Serialize(new { Status = before.ToString() }),
                        JsonSerializer.Serialize(
                            new
                            {
                                Status = maintenance.Status.ToString(),
                                item.Condition,
                                item.IsMaintenanceHold,
                            }
                        )
                    )
                );
                await _unitOfWork.SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                if (input.Status == MaintenanceStatus.Closed)
                {
                    return Outcome(
                        true,
                        $"Repair recorded. {item.Name} ({item.AssetCode}) is back in service in {item.Condition} condition."
                    );
                }

                var statusLabel = input.Status == MaintenanceStatus.InProgress ? "In progress" : "Open";
                return Outcome(true, $"Maintenance saved for {item.Name} ({item.AssetCode}). Status: {statusLabel}.");
            }
            catch (Exception exception)
                when (exception
                        is InvalidOperationException
                            or PersistenceException
                            {
                                Failure: PersistenceFailure.StaleWrite or PersistenceFailure.Duplicate or PersistenceFailure.Busy
                            }
                )
            {
                return Outcome(false, GetSaveError(exception));
            }
        }

        private static OperationResult<bool> Outcome(bool ok, string message, string? field = null, OperationOutcome? status = null)
        {
            return new(
                status ?? (ok ? OperationOutcome.Success : OperationOutcome.Invalid),
                ok,
                message,
                ok ? null : [new(field ?? "", message)]
            );
        }

        private static string GetSaveError(Exception exception)
        {
            if (exception is not PersistenceException persistence)
            {
                return exception.Message;
            }
            return persistence.Failure == PersistenceFailure.Busy ? MaintenanceMessages.RecordBusy : MaintenanceMessages.StaleRecord;
        }
    }
}
