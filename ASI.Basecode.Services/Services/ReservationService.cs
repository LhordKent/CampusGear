using System.Data;
using System.Text.Json;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Identity;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Owns all reservation and loan state changes. Every change that can affect an item's
    /// availability takes the same transaction-owned SQL Server application lock.
    /// </summary>
    public sealed class ReservationService : ServiceBase, IReservationService
    {
        private readonly IReservationRepository _records;
        private readonly IEquipmentRepository _equipmentRepository;
        private readonly IBorrowerProfileRepository _profiles;
        private readonly IUserRepository _accountRepository;
        private readonly ILoanRepository _loans;
        private readonly IMaintenanceRepository _maintenanceRepository;
        private readonly IAuditRepository _auditRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWorkflowLockManager _lockManager;
        private readonly UserManager<User> _userManager;
        private readonly TimeProvider _timeProvider;

        public ReservationService(
            IReservationRepository records,
            IEquipmentRepository equipment,
            IBorrowerProfileRepository profiles,
            IUserRepository accounts,
            ILoanRepository loans,
            IMaintenanceRepository maintenance,
            IAuditRepository audits,
            IUnitOfWork work,
            IWorkflowLockManager locks,
            UserManager<User> users,
            TimeProvider clock
        )
        {
            _records = records;
            _equipmentRepository = equipment;
            _profiles = profiles;
            _accountRepository = accounts;
            _loans = loans;
            _maintenanceRepository = maintenance;
            _auditRepository = audits;
            _unitOfWork = work;
            _lockManager = locks;
            _userManager = users;
            _timeProvider = clock;
        }

        private static readonly TimeSpan PendingHold = TimeSpan.FromHours(24);
        private const int ItemLockTimeoutMilliseconds = 10_000;

        public async Task<ReservationResult> CreateAsync(
            ReservationActor actor,
            CreateReservationCommand command,
            CancellationToken cancellationToken = default
        )
        {
            var now = UtcNow();
            var purpose = RequiredText(command.Purpose, 500, Errors.ReservationPurposeRequired);
            var notes = OptionalText(command.Notes, 2000);
            if (
                command.RequestId == Guid.Empty
                || command.EquipmentItemId == Guid.Empty
                || string.IsNullOrWhiteSpace(command.BorrowerUserId)
                || command.StartAtUtc.Kind != DateTimeKind.Utc
                || command.EndAtUtc.Kind != DateTimeKind.Utc
                || command.StartAtUtc >= command.EndAtUtc
            )
            {
                throw Error(ReservationErrorCode.InvalidInput, Errors.InvalidBookingInput);
            }

            // Only a borrower may reserve, and only for themselves. Staff never take part in creating reservations.
            var actorUser = await RequireActiveVerifiedUserAsync(actor, cancellationToken);
            if (actorUser.Id != command.BorrowerUserId || !await _userManager.IsInRoleAsync(actorUser, RoleNames.Borrower))
            {
                throw Error(ReservationErrorCode.Forbidden, Errors.BorrowerOwnershipRequired);
            }

            var borrower = actorUser;
            if (!borrower.IsActive || !borrower.EmailConfirmed || !await _userManager.IsInRoleAsync(borrower, RoleNames.Borrower))
            {
                throw Error(ReservationErrorCode.IneligibleBorrower, Errors.IneligibleBorrower);
            }

            var existing = await _records.FindByRequestIdAsync(command.RequestId, cancellationToken);
            if (existing is not null)
            {
                return ExistingRequestResult(existing, borrower.Id, command, purpose, notes);
            }

            await using var transaction = await BeginItemTransactionAsync(command.EquipmentItemId, cancellationToken);
            now = UtcNow();
            if (command.StartAtUtc <= now)
            {
                throw Error(ReservationErrorCode.InvalidInput, Errors.StartTimePassed);
            }

            // The second lookup is essential: another request may have committed while we waited for the item lock.
            existing = await _records.FindByRequestIdAsync(command.RequestId, cancellationToken);
            if (existing is not null)
            {
                return ExistingRequestResult(existing, borrower.Id, command, purpose, notes);
            }

            var profileId = await _profiles.GetIdAsync(borrower.Id, cancellationToken);
            if (profileId is null)
            {
                throw Error(ReservationErrorCode.IneligibleBorrower, Errors.BorrowerProfileRequired);
            }

            await AcquireBorrowerLockAsync(profileId.Value, cancellationToken);
            var profile = await RequireEligibleBorrowerAsync(profileId.Value, cancellationToken);
            now = UtcNow();
            if (command.StartAtUtc <= now)
            {
                throw Error(ReservationErrorCode.InvalidInput, Errors.StartTimePassed);
            }

            var item = await _equipmentRepository.FindAsync(command.EquipmentItemId, cancellationToken);
            EnsureUsable(item);
            if (await HasConflictAsync(command.EquipmentItemId, command.StartAtUtc, command.EndAtUtc, now, null, cancellationToken))
            {
                throw Error(ReservationErrorCode.ScheduleConflict, Errors.ScheduleUnavailable);
            }

            TouchItem(item!, now);
            var reservation = new Reservation
            {
                RequestId = command.RequestId,
                BorrowerProfileId = profile.Id,
                EquipmentItemId = command.EquipmentItemId,
                StartAtUtc = command.StartAtUtc,
                EndAtUtc = command.EndAtUtc,
                HoldExpiresAtUtc = Earlier(now.Add(PendingHold), command.StartAtUtc),
                Status = ReservationStatus.Pending,
                Purpose = purpose,
                Notes = notes,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            _records.Add(reservation);
            AddAudit(actor, "reservation.created", reservation, null, now);
            try
            {
                await SaveAsync(cancellationToken);
            }
            catch (PersistenceException exception) when (exception.Failure == PersistenceFailure.Duplicate)
            {
                throw Error(ReservationErrorCode.DuplicateRequest, Errors.AlreadySubmitted);
            }

            await transaction.CommitAsync(cancellationToken);
            return ToResult(reservation);
        }

        public async Task<ReservationResult> ApproveAsync(
            ReservationActor actor,
            Guid reservationId,
            CancellationToken cancellationToken = default
        )
        {
            await RequireCustodianAsync(actor, cancellationToken);
            var itemId = await GetItemIdAsync(reservationId, cancellationToken);
            await using var transaction = await BeginItemTransactionAsync(itemId, cancellationToken);
            var reservation = await GetLockedReservationAsync(reservationId, cancellationToken);
            var now = UtcNow();
            if (reservation.Status == ReservationStatus.Approved)
            {
                return ToResult(reservation);
            }

            if (await ExpireIfDueAsync(reservation, actor, now, transaction, cancellationToken))
            {
                return ToResult(reservation);
            }

            if (reservation.Status != ReservationStatus.Pending)
            {
                throw Error(ReservationErrorCode.InvalidState, Errors.PendingApprovalRequired);
            }

            await AcquireBorrowerLockAsync(reservation.BorrowerProfileId, cancellationToken);
            now = UtcNow();
            if (await ExpireIfDueAsync(reservation, actor, now, transaction, cancellationToken))
            {
                return ToResult(reservation);
            }

            await RequireEligibleBorrowerAsync(reservation.BorrowerProfileId, cancellationToken);
            EnsureUsable(reservation.EquipmentItem);
            if (await HasConflictAsync(itemId, reservation.StartAtUtc, reservation.EndAtUtc, now, reservation.Id, cancellationToken))
            {
                throw Error(ReservationErrorCode.ScheduleConflict, Errors.ScheduleConflict);
            }

            TouchItem(reservation.EquipmentItem, now);
            var before = reservation.Status;
            reservation.Status = ReservationStatus.Approved;
            reservation.HoldExpiresAtUtc = null;
            reservation.DecidedByUserId = actor.UserId;
            reservation.DecidedAtUtc = now;
            reservation.UpdatedAtUtc = now;
            AddAudit(actor, "reservation.approved", reservation, before, now);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(reservation);
        }

        public async Task<ReservationResult> RejectAsync(
            ReservationActor actor,
            Guid reservationId,
            string reason,
            CancellationToken cancellationToken = default
        )
        {
            await RequireCustodianAsync(actor, cancellationToken);
            var decisionReason = RequiredText(reason, 1000, Errors.RejectionReasonRequired);
            var itemId = await GetItemIdAsync(reservationId, cancellationToken);
            await using var transaction = await BeginItemTransactionAsync(itemId, cancellationToken);
            var reservation = await GetLockedReservationAsync(reservationId, cancellationToken);
            var now = UtcNow();
            if (reservation.Status == ReservationStatus.Rejected)
            {
                return ToResult(reservation);
            }

            if (await ExpireIfDueAsync(reservation, actor, now, transaction, cancellationToken))
            {
                return ToResult(reservation);
            }

            if (reservation.Status != ReservationStatus.Pending)
            {
                throw Error(ReservationErrorCode.InvalidState, Errors.PendingRejectionRequired);
            }

            var before = reservation.Status;
            reservation.Status = ReservationStatus.Rejected;
            reservation.HoldExpiresAtUtc = null;
            reservation.DecisionReason = decisionReason;
            reservation.DecidedByUserId = actor.UserId;
            reservation.DecidedAtUtc = now;
            reservation.UpdatedAtUtc = now;
            AddAudit(actor, "reservation.rejected", reservation, before, now);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(reservation);
        }

        public async Task<ReservationResult> CancelAsync(
            ReservationActor actor,
            Guid reservationId,
            string? reason = null,
            CancellationToken cancellationToken = default
        )
        {
            var actorUser = await RequireActiveVerifiedUserAsync(actor, cancellationToken);
            var itemId = await GetItemIdAsync(reservationId, cancellationToken);
            await using var transaction = await BeginItemTransactionAsync(itemId, cancellationToken);
            var reservation = await GetLockedReservationAsync(reservationId, cancellationToken);
            var now = UtcNow();
            // Only the borrower who made a request may cancel it; the automatic batch withdrawal also runs as that borrower.
            if (reservation.BorrowerProfile.UserId != actorUser.Id)
            {
                throw Error(ReservationErrorCode.Forbidden, Errors.CancellationForbidden);
            }

            if (reservation.Status == ReservationStatus.Cancelled)
            {
                return ToResult(reservation);
            }

            if (await ExpireIfDueAsync(reservation, actor, now, transaction, cancellationToken))
            {
                return ToResult(reservation);
            }

            if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Approved))
            {
                throw Error(ReservationErrorCode.InvalidState, Errors.CancellationUnavailable);
            }

            var before = reservation.Status;
            reservation.Status = ReservationStatus.Cancelled;
            reservation.HoldExpiresAtUtc = null;
            reservation.CancelledAtUtc = now;
            reservation.DecisionReason = OptionalText(reason, 1000);
            reservation.UpdatedAtUtc = now;
            AddAudit(actor, "reservation.cancelled", reservation, before, now);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(reservation);
        }

        public async Task<ReservationResult> ReleaseAsync(
            ReservationActor actor,
            Guid reservationId,
            EquipmentCondition conditionAtRelease,
            string? notes = null,
            CancellationToken cancellationToken = default
        )
        {
            await RequireCustodianAsync(actor, cancellationToken);
            if (!Enum.IsDefined(conditionAtRelease) || conditionAtRelease is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable)
            {
                throw Error(ReservationErrorCode.UnavailableEquipment, Errors.DamagedReleaseForbidden);
            }

            var releaseNotes = OptionalText(notes, 2000);
            var itemId = await GetItemIdAsync(reservationId, cancellationToken);
            await using var transaction = await BeginItemTransactionAsync(itemId, cancellationToken);
            var reservation = await GetLockedReservationAsync(reservationId, cancellationToken);
            var now = UtcNow();
            if (reservation.Status == ReservationStatus.Released && reservation.Loan is { ReturnedAtUtc: null })
            {
                return ToResult(reservation);
            }

            if (reservation.Status != ReservationStatus.Approved)
            {
                throw Error(ReservationErrorCode.InvalidState, Errors.ApprovalRequired);
            }

            if (now < reservation.StartAtUtc || now >= reservation.EndAtUtc)
            {
                throw Error(ReservationErrorCode.InvalidState, Errors.ReleaseOutsideBooking);
            }

            await AcquireBorrowerLockAsync(reservation.BorrowerProfileId, cancellationToken);
            now = UtcNow();
            if (now < reservation.StartAtUtc || now >= reservation.EndAtUtc)
            {
                throw Error(ReservationErrorCode.InvalidState, Errors.ReleaseOutsideBooking);
            }

            await RequireEligibleBorrowerAsync(reservation.BorrowerProfileId, cancellationToken);
            EnsureUsable(reservation.EquipmentItem);
            if (
                await HasConflictAsync(itemId, reservation.StartAtUtc, reservation.EndAtUtc, now, reservation.Id, cancellationToken)
                || await _loans.HasOutstandingExceptAsync(itemId, reservation.Id, cancellationToken)
            )
            {
                throw Error(ReservationErrorCode.ScheduleConflict, Errors.ReservationEquipmentUnavailable);
            }

            var loan = new Loan
            {
                ReservationId = reservation.Id,
                ReleasedAtUtc = now,
                DueAtUtc = reservation.EndAtUtc,
                ReleasedByUserId = actor.UserId,
                ConditionAtRelease = conditionAtRelease,
                ReleaseNotes = releaseNotes,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            var before = reservation.Status;
            reservation.Status = ReservationStatus.Released;
            reservation.UpdatedAtUtc = now;
            reservation.EquipmentItem.Condition = conditionAtRelease;
            TouchItem(reservation.EquipmentItem, now);
            reservation.Loan = loan;
            _loans.Add(loan);
            AddAudit(actor, "reservation.released", reservation, before, now);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(reservation);
        }

        public async Task<ReservationResult> ReturnAsync(
            ReservationActor actor,
            Guid reservationId,
            EquipmentCondition conditionAtReturn,
            string? notes = null,
            CancellationToken cancellationToken = default
        )
        {
            await RequireCustodianAsync(actor, cancellationToken);
            if (!Enum.IsDefined(conditionAtReturn))
            {
                throw Error(ReservationErrorCode.InvalidInput, Errors.InvalidReturnCondition);
            }

            var returnNotes = OptionalText(notes, 2000);
            var itemId = await GetItemIdAsync(reservationId, cancellationToken);
            await using var transaction = await BeginItemTransactionAsync(itemId, cancellationToken);
            var reservation = await GetLockedReservationAsync(reservationId, cancellationToken);
            var now = UtcNow();
            if (reservation.Status == ReservationStatus.Completed && reservation.Loan?.ReturnedAtUtc is not null)
            {
                return ToResult(reservation);
            }

            if (reservation.Status != ReservationStatus.Released || reservation.Loan is null || reservation.Loan.ReturnedAtUtc is not null)
            {
                throw Error(ReservationErrorCode.InvalidState, Errors.ActiveLoanRequired);
            }

            var loan = reservation.Loan;
            if (now < loan.ReleasedAtUtc)
            {
                throw Error(ReservationErrorCode.InvalidState, Errors.ReturnBeforeRelease);
            }

            loan.ReturnedAtUtc = now;
            loan.ReceivedByUserId = actor.UserId;
            loan.ConditionAtReturn = conditionAtReturn;
            loan.ReturnNotes = returnNotes;
            loan.UpdatedAtUtc = now;
            var item = reservation.EquipmentItem;
            item.Condition = conditionAtReturn;
            TouchItem(item, now);
            if (conditionAtReturn is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable)
            {
                item.IsMaintenanceHold = true;
                _maintenanceRepository.Add(
                    new MaintenanceCase
                    {
                        EquipmentItemId = item.Id,
                        LoanId = loan.Id,
                        Status = MaintenanceStatus.Open,
                        Description = $"Returned {conditionAtReturn} after loan {loan.Id}.",
                        OpenedAtUtc = now,
                        OpenedByUserId = actor.UserId,
                    }
                );
            }

            var before = reservation.Status;
            reservation.Status = ReservationStatus.Completed;
            reservation.UpdatedAtUtc = now;
            AddAudit(actor, "reservation.returned", reservation, before, now);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(reservation);
        }

        public async Task<int> ExpireDueAsync(int batchSize = 100, CancellationToken cancellationToken = default)
        {
            if (batchSize is < 1 or > 1000)
            {
                throw Error(ReservationErrorCode.InvalidInput, Errors.InvalidBatchSize);
            }

            var now = UtcNow();
            var candidateIds = await _records.ExpiryCandidatesAsync(now, batchSize, cancellationToken);
            var expired = 0;
            foreach (var candidate in candidateIds)
            {
                await using var transaction = await BeginItemTransactionAsync(candidate.EquipmentItemId, cancellationToken);
                var reservation = await _records.FindAsync(candidate.Id, cancellationToken);
                if (reservation is null || !IsDueToExpire(reservation, now))
                {
                    continue;
                }

                Expire(reservation, null, now);
                await SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                expired++;
            }

            return expired;
        }

        private async Task<bool> ExpireIfDueAsync(
            Reservation reservation,
            ReservationActor actor,
            DateTime now,
            IDataTransaction transaction,
            CancellationToken cancellationToken
        )
        {
            if (!IsDueToExpire(reservation, now))
            {
                return false;
            }

            Expire(reservation, actor, now);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        private void Expire(Reservation reservation, ReservationActor? actor, DateTime now)
        {
            var before = reservation.Status;
            reservation.Status = ReservationStatus.Expired;
            reservation.HoldExpiresAtUtc = null;
            reservation.UpdatedAtUtc = now;
            AddAudit(actor, "reservation.expired", reservation, before, now);
        }

        private static bool IsDueToExpire(Reservation reservation, DateTime now)
        {
            return reservation.Status == ReservationStatus.Pending
                && (reservation.HoldExpiresAtUtc is null || reservation.HoldExpiresAtUtc <= now || reservation.StartAtUtc <= now);
        }

        private Task<bool> HasConflictAsync(
            Guid itemId,
            DateTime startAtUtc,
            DateTime endAtUtc,
            DateTime now,
            Guid? excludingReservationId,
            CancellationToken cancellationToken
        )
        {
            return _records.HasConflictAsync(itemId, startAtUtc, endAtUtc, now, excludingReservationId, cancellationToken);
        }

        private async Task<IDataTransaction> BeginItemTransactionAsync(Guid itemId, CancellationToken cancellationToken)
        {
            var transaction = await _unitOfWork.BeginAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                await AcquireTransactionLockAsync($"CampusGear:EquipmentItem:{itemId:N}", cancellationToken);
                return transaction;
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }

        private Task AcquireBorrowerLockAsync(Guid profileId, CancellationToken cancellationToken)
        {
            return AcquireTransactionLockAsync($"CampusGear:BorrowerProfile:{profileId:N}", cancellationToken);
        }

        private async Task AcquireTransactionLockAsync(string lockResource, CancellationToken cancellationToken)
        {
            try
            {
                await _lockManager.AcquireAsync(lockResource, cancellationToken);
            }
            catch (PersistenceException e) when (e.Failure == PersistenceFailure.Busy)
            {
                throw Error(ReservationErrorCode.Busy, Errors.ReservationRecordBusy);
            }
        }

        private async Task<BorrowerProfile> RequireEligibleBorrowerAsync(Guid profileId, CancellationToken cancellationToken)
        {
            // UserManager may already track a user from before the lock wait. This query must
            // read fresh profile/account data and role membership after acquiring the lock.
            var profile = await _profiles.FindFreshAsync(profileId, cancellationToken);
            if (profile is null || !profile.IsEligible || !profile.User.IsActive || !profile.User.EmailConfirmed)
            {
                throw Error(ReservationErrorCode.IneligibleBorrower, Errors.BorrowerNoLongerEligible);
            }

            var isBorrower = await _accountRepository.HasBorrowerRoleAsync(profile.UserId, cancellationToken);
            if (!isBorrower)
            {
                throw Error(ReservationErrorCode.IneligibleBorrower, Errors.BorrowerRoleRequired);
            }

            return profile;
        }

        private async Task<Guid> GetItemIdAsync(Guid reservationId, CancellationToken cancellationToken)
        {
            if (reservationId == Guid.Empty)
            {
                throw Error(ReservationErrorCode.InvalidInput, Errors.ReservationIdRequired);
            }

            var itemId = await _records.GetItemIdAsync(reservationId, cancellationToken);
            return itemId ?? throw Error(ReservationErrorCode.NotFound, Errors.ReservationNotFound);
        }

        private async Task<Reservation> GetLockedReservationAsync(Guid reservationId, CancellationToken cancellationToken)
        {
            return await _records.FindWorkflowAsync(reservationId, cancellationToken)
                ?? throw Error(ReservationErrorCode.NotFound, Errors.ReservationNotFound);
        }

        private async Task<User> RequireActiveVerifiedUserAsync(ReservationActor actor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(actor.UserId))
            {
                throw Error(ReservationErrorCode.Forbidden, Errors.SignInRequired);
            }

            var user = await _userManager.FindByIdAsync(actor.UserId);
            if (user is null || !user.IsActive || !user.EmailConfirmed)
            {
                throw Error(ReservationErrorCode.Forbidden, Errors.VerifiedAccountRequired);
            }

            return user;
        }

        /// <summary>Approval, rejection, release and return belong to the custodian alone; administrators do not take part in the borrowing flow.</summary>
        private async Task RequireCustodianAsync(ReservationActor actor, CancellationToken cancellationToken)
        {
            var user = await RequireActiveVerifiedUserAsync(actor, cancellationToken);
            if (!await _userManager.IsInRoleAsync(user, RoleNames.Custodian))
            {
                throw Error(ReservationErrorCode.Forbidden, Errors.CustodianRequired);
            }
        }

        private async Task SaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _unitOfWork.SaveAsync(cancellationToken);
            }
            catch (PersistenceException e) when (e.Failure == PersistenceFailure.StaleWrite)
            {
                throw Error(ReservationErrorCode.StaleWrite, Errors.StaleRecord);
            }
        }

        private void AddAudit(ReservationActor? actor, string action, Reservation reservation, ReservationStatus? before, DateTime now)
        {
            _auditRepository.Add(
                new AuditEvent
                {
                    ActorUserId = actor?.UserId,
                    Action = action,
                    EntityType = nameof(Reservation),
                    EntityId = reservation.Id.ToString("D"),
                    Outcome = "Succeeded",
                    BeforeJson = before is null ? null : JsonSerializer.Serialize(new { Status = before.ToString() }),
                    AfterJson = JsonSerializer.Serialize(
                        new
                        {
                            Status = reservation.Status.ToString(),
                            reservation.EquipmentItemId,
                            reservation.BorrowerProfileId,
                            reservation.StartAtUtc,
                            reservation.EndAtUtc,
                            reservation.HoldExpiresAtUtc,
                            LoanId = reservation.Loan?.Id,
                            ReturnedAtUtc = reservation.Loan?.ReturnedAtUtc,
                            ConditionAtReturn = reservation.Loan?.ConditionAtReturn?.ToString(),
                        }
                    ),
                    IpAddress = actor?.IpAddress,
                    CorrelationId = actor?.CorrelationId,
                    CreatedAtUtc = now,
                }
            );
        }

        private static ReservationResult ExistingRequestResult(
            Reservation existing,
            string borrowerUserId,
            CreateReservationCommand command,
            string purpose,
            string? notes
        )
        {
            if (
                existing.BorrowerProfile.UserId != borrowerUserId
                || existing.EquipmentItemId != command.EquipmentItemId
                || existing.StartAtUtc != command.StartAtUtc
                || existing.EndAtUtc != command.EndAtUtc
                || existing.Purpose != purpose
                || existing.Notes != notes
            )
            {
                throw Error(ReservationErrorCode.DuplicateRequest, Errors.DuplicateRequestId);
            }

            return ToResult(existing);
        }

        private static ReservationResult ToResult(Reservation reservation)
        {
            return new(
                reservation.Id,
                reservation.RequestId,
                reservation.EquipmentItemId,
                reservation.BorrowerProfileId,
                reservation.Status,
                AsUtc(reservation.StartAtUtc),
                AsUtc(reservation.EndAtUtc),
                AsUtc(reservation.HoldExpiresAtUtc),
                reservation.Loan?.Id,
                AsUtc(reservation.Loan?.ReleasedAtUtc),
                AsUtc(reservation.Loan?.DueAtUtc),
                AsUtc(reservation.Loan?.ReturnedAtUtc),
                reservation.Loan?.ConditionAtReturn
            );
        }

        private static void EnsureUsable(EquipmentItem? item)
        {
            if (
                item is null
                || !item.IsActive
                || item.IsMaintenanceHold
                || item.Condition is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable
            )
            {
                throw Error(ReservationErrorCode.UnavailableEquipment, Errors.ItemNotBookable);
            }
        }

        private void TouchItem(EquipmentItem item, DateTime now)
        {
            // Its rowversion detects an inventory/maintenance edit made concurrently by a path
            // that does not yet participate in the per-item application lock.
            item.UpdatedAtUtc = now;
            _equipmentRepository.MarkUpdated(item);
        }

        private static string RequiredText(string? value, int maxLength, string emptyMessage)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                throw Error(ReservationErrorCode.InvalidInput, emptyMessage);
            }

            if (text.Length > maxLength)
            {
                throw Error(ReservationErrorCode.InvalidInput, $"Text must be {maxLength} characters or fewer.");
            }

            return text;
        }

        private static string? OptionalText(string? value, int maxLength)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            if (text.Length > maxLength)
            {
                throw Error(ReservationErrorCode.InvalidInput, $"Text must be {maxLength} characters or fewer.");
            }

            return text;
        }

        private static DateTime Earlier(DateTime first, DateTime second) => first <= second ? first : second;

        // SQL Server datetime2 values return with Kind=Unspecified. The column contract is UTC.
        private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static DateTime? AsUtc(DateTime? value)
        {
            return value is { } date ? AsUtc(date) : null;
        }

        private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

        private static ReservationWorkflowException Error(ReservationErrorCode code, string message) => new(code, message);
    }
}
