using CampusGear.Models;

namespace CampusGear.Services.Reservations;

// Actor.UserId must come from the authenticated principal, never from a form field.
public sealed record ReservationActor(
    string UserId,
    string? IpAddress = null,
    string? CorrelationId = null);

public sealed record CreateReservationCommand(
    Guid RequestId,
    string BorrowerUserId,
    Guid EquipmentItemId,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Purpose,
    string? Notes = null);

public sealed record ReservationResult(
    Guid Id,
    Guid RequestId,
    Guid EquipmentItemId,
    Guid BorrowerProfileId,
    ReservationStatus Status,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    DateTime? HoldExpiresAtUtc,
    Guid? LoanId,
    DateTime? ReleasedAtUtc,
    DateTime? DueAtUtc,
    DateTime? ReturnedAtUtc,
    EquipmentCondition? ConditionAtReturn);

public enum ReservationErrorCode
{
    InvalidInput,
    Forbidden,
    NotFound,
    IneligibleBorrower,
    UnavailableEquipment,
    ScheduleConflict,
    InvalidState,
    HoldExpired,
    DuplicateRequest,
    Busy,
    StaleWrite
}

public sealed class ReservationWorkflowException(
    ReservationErrorCode code,
    string message) : Exception(message)
{
    public ReservationErrorCode Code { get; } = code;
}

public interface IReservationService
{
    Task<ReservationResult> CreateAsync(
        ReservationActor actor, CreateReservationCommand command, CancellationToken cancellationToken = default);

    Task<ReservationResult> ApproveAsync(
        ReservationActor actor, Guid reservationId, CancellationToken cancellationToken = default);

    Task<ReservationResult> RejectAsync(
        ReservationActor actor, Guid reservationId, string reason, CancellationToken cancellationToken = default);

    Task<ReservationResult> CancelAsync(
        ReservationActor actor, Guid reservationId, string? reason = null, CancellationToken cancellationToken = default);

    Task<ReservationResult> ReleaseAsync(
        ReservationActor actor, Guid reservationId, EquipmentCondition conditionAtRelease,
        string? notes = null, CancellationToken cancellationToken = default);

    Task<ReservationResult> ReturnAsync(
        ReservationActor actor, Guid reservationId, EquipmentCondition conditionAtReturn,
        string? notes = null, CancellationToken cancellationToken = default);

    Task<int> ExpireDueAsync(int batchSize = 100, CancellationToken cancellationToken = default);
}
