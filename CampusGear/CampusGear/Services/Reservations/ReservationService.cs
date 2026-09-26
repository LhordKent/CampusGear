using System.Data;
using System.Text.Json;
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CampusGear.Services.Reservations;

/// <summary>
/// Owns all reservation and loan state changes. Every change that can affect an item's
/// availability takes the same transaction-owned SQL Server application lock.
/// </summary>
public sealed class ReservationService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    TimeProvider clock) : IReservationService
{
    private static readonly TimeSpan PendingHold = TimeSpan.FromHours(24);
    private const int ItemLockTimeoutMilliseconds = 10_000;

    public async Task<ReservationResult> CreateAsync(
        ReservationActor actor, CreateReservationCommand command, CancellationToken cancellationToken = default)
    {
        var now = UtcNow();
        var purpose = RequiredText(command.Purpose, 500, "Purpose is required.");
        var notes = OptionalText(command.Notes, 2000);
        if (command.RequestId == Guid.Empty || command.EquipmentItemId == Guid.Empty ||
            string.IsNullOrWhiteSpace(command.BorrowerUserId) ||
            command.StartAtUtc.Kind != DateTimeKind.Utc || command.EndAtUtc.Kind != DateTimeKind.Utc ||
            command.StartAtUtc >= command.EndAtUtc)
        {
            throw Error(ReservationErrorCode.InvalidInput, "Choose an item and a future time range in UTC.");
        }

        var actorUser = await RequireActiveVerifiedUserAsync(actor, cancellationToken);
        var isSelf = actorUser.Id == command.BorrowerUserId;
        if (isSelf)
        {
            if (!await users.IsInRoleAsync(actorUser, "Borrower"))
                throw Error(ReservationErrorCode.Forbidden, "Only borrowers can request equipment for themselves.");
        }
        else if (!await users.IsInRoleAsync(actorUser, "Administrator"))
        {
            throw Error(ReservationErrorCode.Forbidden, "Only administrators can create requests for another borrower.");
        }

        var borrower = isSelf
            ? actorUser
            : await users.FindByIdAsync(command.BorrowerUserId);
        if (borrower is null || !borrower.IsActive || !borrower.EmailConfirmed ||
            !await users.IsInRoleAsync(borrower, "Borrower"))
        {
            throw Error(ReservationErrorCode.IneligibleBorrower, "The borrower account is not eligible to reserve equipment.");
        }

        var existing = await db.Reservations.AsNoTracking()
            .Include(x => x.BorrowerProfile).Include(x => x.Loan)
            .SingleOrDefaultAsync(x => x.RequestId == command.RequestId, cancellationToken);
        if (existing is not null)
            return ExistingRequestResult(existing, borrower.Id, command, purpose, notes);

        await using var transaction = await BeginItemTransactionAsync(command.EquipmentItemId, cancellationToken);
        now = UtcNow();
        if (command.StartAtUtc <= now)
            throw Error(ReservationErrorCode.InvalidInput, "The requested start time has passed.");

        // The second lookup is essential: another request may have committed while we waited for the item lock.
        existing = await db.Reservations.AsNoTracking()
            .Include(x => x.BorrowerProfile).Include(x => x.Loan)
            .SingleOrDefaultAsync(x => x.RequestId == command.RequestId, cancellationToken);
        if (existing is not null)
            return ExistingRequestResult(existing, borrower.Id, command, purpose, notes);

        var profileId = await db.BorrowerProfiles.AsNoTracking()
            .Where(x => x.UserId == borrower.Id).Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (profileId is null)
            throw Error(ReservationErrorCode.IneligibleBorrower, "The borrower has no borrowing profile.");
        await AcquireBorrowerLockAsync(profileId.Value, cancellationToken);
        var profile = await RequireEligibleBorrowerAsync(profileId.Value, cancellationToken);
        now = UtcNow();
        if (command.StartAtUtc <= now)
            throw Error(ReservationErrorCode.InvalidInput, "The requested start time has passed.");

        var item = await db.EquipmentItems.SingleOrDefaultAsync(
            x => x.Id == command.EquipmentItemId, cancellationToken);
        EnsureUsable(item);

        if (await HasConflictAsync(command.EquipmentItemId, command.StartAtUtc,
                command.EndAtUtc, now, null, cancellationToken))
        {
            throw Error(ReservationErrorCode.ScheduleConflict, "This equipment is unavailable for the requested time.");
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
            UpdatedAtUtc = now
        };
        db.Reservations.Add(reservation);
        AddAudit(actor, "reservation.created", reservation, null, now);

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueKeyViolation(exception))
        {
            throw Error(ReservationErrorCode.DuplicateRequest, "This request was already submitted.");
        }

        await transaction.CommitAsync(cancellationToken);
        return ToResult(reservation);
    }

    public async Task<ReservationResult> ApproveAsync(
        ReservationActor actor, Guid reservationId, CancellationToken cancellationToken = default)
    {
        await RequireStaffAsync(actor, cancellationToken);
        var itemId = await GetItemIdAsync(reservationId, cancellationToken);
        await using var transaction = await BeginItemTransactionAsync(itemId, cancellationToken);
        var reservation = await GetLockedReservationAsync(reservationId, cancellationToken);
        var now = UtcNow();

        if (reservation.Status == ReservationStatus.Approved)
            return ToResult(reservation);
        if (await ExpireIfDueAsync(reservation, actor, now, transaction, cancellationToken))
            return ToResult(reservation);
        if (reservation.Status != ReservationStatus.Pending)
            throw Error(ReservationErrorCode.InvalidState, "Only pending requests can be approved.");

        await AcquireBorrowerLockAsync(reservation.BorrowerProfileId, cancellationToken);
        now = UtcNow();
        if (await ExpireIfDueAsync(reservation, actor, now, transaction, cancellationToken))
            return ToResult(reservation);
        await RequireEligibleBorrowerAsync(reservation.BorrowerProfileId, cancellationToken);
        EnsureUsable(reservation.EquipmentItem);
        if (await HasConflictAsync(itemId, reservation.StartAtUtc, reservation.EndAtUtc,
                now, reservation.Id, cancellationToken))
        {
            throw Error(ReservationErrorCode.ScheduleConflict, "The equipment is no longer available for this time.");
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
        ReservationActor actor, Guid reservationId, string reason, CancellationToken cancellationToken = default)
    {
        await RequireStaffAsync(actor, cancellationToken);
        var decisionReason = RequiredText(reason, 1000, "A rejection reason is required.");
        var itemId = await GetItemIdAsync(reservationId, cancellationToken);
        await using var transaction = await BeginItemTransactionAsync(itemId, cancellationToken);
        var reservation = await GetLockedReservationAsync(reservationId, cancellationToken);
        var now = UtcNow();

        if (reservation.Status == ReservationStatus.Rejected)
            return ToResult(reservation);
        if (await ExpireIfDueAsync(reservation, actor, now, transaction, cancellationToken))
            return ToResult(reservation);
        if (reservation.Status != ReservationStatus.Pending)
            throw Error(ReservationErrorCode.InvalidState, "Only pending requests can be rejected.");

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
        ReservationActor actor, Guid reservationId, string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var actorUser = await RequireActiveVerifiedUserAsync(actor, cancellationToken);
        var itemId = await GetItemIdAsync(reservationId, cancellationToken);
        await using var transaction = await BeginItemTransactionAsync(itemId, cancellationToken);
        var reservation = await GetLockedReservationAsync(reservationId, cancellationToken);
        var now = UtcNow();

        if (reservation.BorrowerProfile.UserId != actorUser.Id &&
            !await users.IsInRoleAsync(actorUser, "Administrator"))
        {
            throw Error(ReservationErrorCode.Forbidden, "You cannot cancel this request.");
        }

        if (reservation.Status == ReservationStatus.Cancelled)
            return ToResult(reservation);
        if (await ExpireIfDueAsync(reservation, actor, now, transaction, cancellationToken))
            return ToResult(reservation);
        if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Approved))
            throw Error(ReservationErrorCode.InvalidState, "This request can no longer be cancelled.");

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
        ReservationActor actor, Guid reservationId, EquipmentCondition conditionAtRelease,
        string? notes = null, CancellationToken cancellationToken = default)
    {
        await RequireStaffAsync(actor, cancellationToken);
        if (!Enum.IsDefined(conditionAtRelease) ||
            conditionAtRelease is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable)
            throw Error(ReservationErrorCode.UnavailableEquipment, "Damaged equipment cannot be released.");

        var releaseNotes = OptionalText(notes, 2000);
        var itemId = await GetItemIdAsync(reservationId, cancellationToken);
        await using var transaction = await BeginItemTransactionAsync(itemId, cancellationToken);
        var reservation = await GetLockedReservationAsync(reservationId, cancellationToken);
        var now = UtcNow();

        if (reservation.Status == ReservationStatus.Released && reservation.Loan is { ReturnedAtUtc: null })
            return ToResult(reservation);
        if (reservation.Status != ReservationStatus.Approved)
            throw Error(ReservationErrorCode.InvalidState, "Only approved requests can be released.");
        if (now < reservation.StartAtUtc || now >= reservation.EndAtUtc)
            throw Error(ReservationErrorCode.InvalidState, "Release must occur during the approved booking time.");

        await AcquireBorrowerLockAsync(reservation.BorrowerProfileId, cancellationToken);
        now = UtcNow();
        if (now < reservation.StartAtUtc || now >= reservation.EndAtUtc)
            throw Error(ReservationErrorCode.InvalidState, "Release must occur during the approved booking time.");
        await RequireEligibleBorrowerAsync(reservation.BorrowerProfileId, cancellationToken);
        EnsureUsable(reservation.EquipmentItem);
        if (await HasConflictAsync(itemId, reservation.StartAtUtc, reservation.EndAtUtc,
                now, reservation.Id, cancellationToken) ||
            await db.Loans.AnyAsync(x => x.Reservation.EquipmentItemId == itemId &&
                    x.ReservationId != reservation.Id && x.ReturnedAtUtc == null, cancellationToken))
        {
            throw Error(ReservationErrorCode.ScheduleConflict, "The equipment is currently unavailable.");
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
            UpdatedAtUtc = now
        };
        var before = reservation.Status;
        reservation.Status = ReservationStatus.Released;
        reservation.UpdatedAtUtc = now;
        reservation.EquipmentItem.Condition = conditionAtRelease;
        TouchItem(reservation.EquipmentItem, now);
        reservation.Loan = loan;
        db.Loans.Add(loan);
        AddAudit(actor, "reservation.released", reservation, before, now);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(reservation);
    }

    public async Task<ReservationResult> ReturnAsync(
        ReservationActor actor, Guid reservationId, EquipmentCondition conditionAtReturn,
        string? notes = null, CancellationToken cancellationToken = default)
    {
        await RequireStaffAsync(actor, cancellationToken);
        if (!Enum.IsDefined(conditionAtReturn))
            throw Error(ReservationErrorCode.InvalidInput, "Choose a valid return condition.");
        var returnNotes = OptionalText(notes, 2000);
        var itemId = await GetItemIdAsync(reservationId, cancellationToken);
        await using var transaction = await BeginItemTransactionAsync(itemId, cancellationToken);
        var reservation = await GetLockedReservationAsync(reservationId, cancellationToken);
        var now = UtcNow();

        if (reservation.Status == ReservationStatus.Completed && reservation.Loan?.ReturnedAtUtc is not null)
            return ToResult(reservation);
        if (reservation.Status != ReservationStatus.Released || reservation.Loan is null ||
            reservation.Loan.ReturnedAtUtc is not null)
        {
            throw Error(ReservationErrorCode.InvalidState, "Only an active loan can be returned.");
        }

        var loan = reservation.Loan;
        if (now < loan.ReleasedAtUtc)
            throw Error(ReservationErrorCode.InvalidState, "Return time cannot precede release.");
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
            db.MaintenanceCases.Add(new MaintenanceCase
            {
                EquipmentItemId = item.Id,
                LoanId = loan.Id,
                Status = MaintenanceStatus.Open,
                Description = $"Returned {conditionAtReturn} after loan {loan.Id}.",
                OpenedAtUtc = now,
                OpenedByUserId = actor.UserId
            });
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
            throw Error(ReservationErrorCode.InvalidInput, "Batch size must be between 1 and 1000.");

        var now = UtcNow();
        var candidateIds = await db.Reservations.AsNoTracking()
            .Where(x => x.Status == ReservationStatus.Pending &&
                (x.HoldExpiresAtUtc == null || x.HoldExpiresAtUtc <= now || x.StartAtUtc <= now))
            .OrderBy(x => x.HoldExpiresAtUtc)
            .Select(x => new { x.Id, x.EquipmentItemId })
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var candidate in candidateIds)
        {
            await using var transaction = await BeginItemTransactionAsync(candidate.EquipmentItemId, cancellationToken);
            var reservation = await db.Reservations.SingleOrDefaultAsync(x => x.Id == candidate.Id, cancellationToken);
            if (reservation is null || !IsDueToExpire(reservation, now))
                continue;

            Expire(reservation, null, now);
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            expired++;
        }

        return expired;
    }

    private async Task<bool> ExpireIfDueAsync(
        Reservation reservation, ReservationActor actor, DateTime now,
        IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        if (!IsDueToExpire(reservation, now))
            return false;

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

    private static bool IsDueToExpire(Reservation reservation, DateTime now) =>
        reservation.Status == ReservationStatus.Pending &&
        (reservation.HoldExpiresAtUtc is null || reservation.HoldExpiresAtUtc <= now ||
         reservation.StartAtUtc <= now);

    private async Task<bool> HasConflictAsync(
        Guid itemId, DateTime startAtUtc, DateTime endAtUtc, DateTime now,
        Guid? excludingReservationId, CancellationToken cancellationToken) =>
        await db.Reservations.AnyAsync(x =>
            x.EquipmentItemId == itemId &&
            (excludingReservationId == null || x.Id != excludingReservationId) &&
            x.StartAtUtc < endAtUtc && x.EndAtUtc > startAtUtc &&
            (x.Status == ReservationStatus.Approved ||
             x.Status == ReservationStatus.Released ||
             x.Status == ReservationStatus.Pending &&
                 x.HoldExpiresAtUtc != null && x.HoldExpiresAtUtc > now && x.StartAtUtc > now),
            cancellationToken);

    private async Task<IDbContextTransaction> BeginItemTransactionAsync(
        Guid itemId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
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

    private Task AcquireBorrowerLockAsync(Guid profileId, CancellationToken cancellationToken) =>
        AcquireTransactionLockAsync($"CampusGear:BorrowerProfile:{profileId:N}", cancellationToken);

    private async Task AcquireTransactionLockAsync(string lockResource, CancellationToken cancellationToken)
    {
        // The transaction owns both locks. Every reservation path takes item before borrower
        // so staff eligibility edits and availability changes have a consistent lock order.
        var result = new SqlParameter("@lockResult", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = lockResource };
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @lockResult = sp_getapplock @Resource = @resource, " +
            "@LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = " +
            ItemLockTimeoutMilliseconds,
            new object[] { result, resource }, cancellationToken);
        if (result.Value is not int code || code < 0)
            throw Error(ReservationErrorCode.Busy, "This record is busy. Please retry the action.");
    }

    private async Task<BorrowerProfile> RequireEligibleBorrowerAsync(
        Guid profileId, CancellationToken cancellationToken)
    {
        // UserManager may already track a user from before the lock wait. This query must
        // read fresh profile/account data and role membership after acquiring the lock.
        var profile = await db.BorrowerProfiles.AsNoTracking().Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == profileId, cancellationToken);
        if (profile is null || !profile.IsEligible || !profile.User.IsActive || !profile.User.EmailConfirmed)
            throw Error(ReservationErrorCode.IneligibleBorrower, "The borrower is no longer eligible to borrow equipment.");
        var isBorrower = await db.UserRoles.AsNoTracking().Join(db.Roles.AsNoTracking(),
                userRole => userRole.RoleId, role => role.Id, (userRole, role) => new { userRole, role })
            .AnyAsync(x => x.userRole.UserId == profile.UserId && x.role.NormalizedName == "BORROWER",
                cancellationToken);
        if (!isBorrower)
            throw Error(ReservationErrorCode.IneligibleBorrower, "The account no longer has borrower access.");
        return profile;
    }

    private async Task<Guid> GetItemIdAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        if (reservationId == Guid.Empty)
            throw Error(ReservationErrorCode.InvalidInput, "A reservation ID is required.");
        var itemId = await db.Reservations.AsNoTracking()
            .Where(x => x.Id == reservationId)
            .Select(x => (Guid?)x.EquipmentItemId)
            .SingleOrDefaultAsync(cancellationToken);
        return itemId ?? throw Error(ReservationErrorCode.NotFound, "Reservation was not found.");
    }

    private async Task<Reservation> GetLockedReservationAsync(
        Guid reservationId, CancellationToken cancellationToken) =>
        await db.Reservations.Include(x => x.Loan).Include(x => x.EquipmentItem)
            .Include(x => x.BorrowerProfile)
            .SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken)
        ?? throw Error(ReservationErrorCode.NotFound, "Reservation was not found.");

    private async Task<ApplicationUser> RequireActiveVerifiedUserAsync(
        ReservationActor actor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(actor.UserId))
            throw Error(ReservationErrorCode.Forbidden, "Sign in is required.");
        var user = await users.FindByIdAsync(actor.UserId);
        if (user is null || !user.IsActive || !user.EmailConfirmed)
            throw Error(ReservationErrorCode.Forbidden, "An active, verified account is required.");
        return user;
    }

    private async Task RequireStaffAsync(ReservationActor actor, CancellationToken cancellationToken)
    {
        var user = await RequireActiveVerifiedUserAsync(actor, cancellationToken);
        if (!await users.IsInRoleAsync(user, "Custodian") &&
            !await users.IsInRoleAsync(user, "Administrator"))
        {
            throw Error(ReservationErrorCode.Forbidden, "Staff access is required.");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Error(ReservationErrorCode.StaleWrite, "This record changed. Reload and retry.");
        }
    }

    private void AddAudit(
        ReservationActor? actor, string action, Reservation reservation,
        ReservationStatus? before, DateTime now)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = actor?.UserId,
            Action = action,
            EntityType = nameof(Reservation),
            EntityId = reservation.Id.ToString("D"),
            Outcome = "Succeeded",
            BeforeJson = before is null ? null : JsonSerializer.Serialize(new { Status = before.ToString() }),
            AfterJson = JsonSerializer.Serialize(new
            {
                Status = reservation.Status.ToString(),
                reservation.EquipmentItemId,
                reservation.BorrowerProfileId,
                reservation.StartAtUtc,
                reservation.EndAtUtc,
                reservation.HoldExpiresAtUtc,
                LoanId = reservation.Loan?.Id,
                ReturnedAtUtc = reservation.Loan?.ReturnedAtUtc,
                ConditionAtReturn = reservation.Loan?.ConditionAtReturn?.ToString()
            }),
            IpAddress = actor?.IpAddress,
            CorrelationId = actor?.CorrelationId,
            CreatedAtUtc = now
        });
    }

    private static ReservationResult ExistingRequestResult(
        Reservation existing, string borrowerUserId, CreateReservationCommand command,
        string purpose, string? notes)
    {
        if (existing.BorrowerProfile.UserId != borrowerUserId ||
            existing.EquipmentItemId != command.EquipmentItemId ||
            existing.StartAtUtc != command.StartAtUtc || existing.EndAtUtc != command.EndAtUtc ||
            existing.Purpose != purpose || existing.Notes != notes)
        {
            throw Error(ReservationErrorCode.DuplicateRequest, "This request ID is already in use.");
        }
        return ToResult(existing);
    }

    private static ReservationResult ToResult(Reservation reservation) => new(
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
        reservation.Loan?.ConditionAtReturn);

    private static void EnsureUsable(EquipmentItem? item)
    {
        if (item is null || !item.IsActive || item.IsMaintenanceHold ||
            item.Condition is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable)
        {
            throw Error(ReservationErrorCode.UnavailableEquipment, "This equipment is not available for borrowing.");
        }
    }

    private void TouchItem(EquipmentItem item, DateTime now)
    {
        // Its rowversion detects an inventory/maintenance edit made concurrently by a path
        // that does not yet participate in the per-item application lock.
        item.UpdatedAtUtc = now;
        db.Entry(item).Property(x => x.UpdatedAtUtc).IsModified = true;
    }

    private static string RequiredText(string? value, int maxLength, string emptyMessage)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            throw Error(ReservationErrorCode.InvalidInput, emptyMessage);
        if (text.Length > maxLength)
            throw Error(ReservationErrorCode.InvalidInput, $"Text must be {maxLength} characters or fewer.");
        return text;
    }

    private static string? OptionalText(string? value, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        if (text.Length > maxLength)
            throw Error(ReservationErrorCode.InvalidInput, $"Text must be {maxLength} characters or fewer.");
        return text;
    }

    private static bool IsUniqueKeyViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && sql.Number is 2601 or 2627;

    private static DateTime Earlier(DateTime first, DateTime second) => first <= second ? first : second;

    // SQL Server datetime2 values return with Kind=Unspecified. The column contract is UTC.
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? AsUtc(DateTime? value) => value is { } date ? AsUtc(date) : null;

    private DateTime UtcNow() => clock.GetUtcNow().UtcDateTime;

    private static ReservationWorkflowException Error(ReservationErrorCode code, string message) => new(code, message);
}
