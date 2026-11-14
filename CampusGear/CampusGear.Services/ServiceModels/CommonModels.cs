namespace CampusGear.Services.ServiceModels
{
    public sealed record FieldError(string Field, string Message);

    public enum OperationOutcome
    {
        Success,
        Invalid,
        NotFound,
        Forbidden,
        Conflict,
        Busy,
    }

    public sealed record OperationResult<T>(
        OperationOutcome Outcome,
        T? Value = default,
        string? Message = null,
        IReadOnlyList<FieldError>? Errors = null,
        bool Reload = false
    )
    {
        public bool Succeeded => Outcome == OperationOutcome.Success;

        public static OperationResult<T> Ok(T? value = default, string? message = null)
        {
            return new(OperationOutcome.Success, value, message);
        }

        public static OperationResult<T> Invalid(string message, string field = "", bool reload = false)
        {
            return new(OperationOutcome.Invalid, Message: message, Errors: [new(field, message)], Reload: reload);
        }

        public static OperationResult<T> Missing() => new(OperationOutcome.NotFound);
    }

    public sealed record RequestContext(string? UserId, string? IpAddress = null, string? CorrelationId = null);
}
