using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.ServiceModels
{
    public sealed record CreateReservationCommand(
        Guid RequestId,
        string BorrowerUserId,
        Guid EquipmentItemId,
        DateTime StartAtUtc,
        DateTime EndAtUtc,
        string Purpose,
        string? Notes = null
    );

    // Actor.UserId must come from the authenticated principal, never from a form field.
    public sealed record ReservationActor(string UserId, string? IpAddress = null, string? CorrelationId = null);

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
        StaleWrite,
    }

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
        EquipmentCondition? ConditionAtReturn
    );

    public sealed class ReservationWorkflowException : Exception
    {
        private readonly ReservationErrorCode _code;
        private readonly string _message;

        public ReservationWorkflowException(ReservationErrorCode code, string message)
            : base(message)
        {
            _code = code;
            _message = message;
            Code = _code;
        }

        public ReservationErrorCode Code { get; }
    }
}
