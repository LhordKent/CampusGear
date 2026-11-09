using CampusGear.Models;

namespace CampusGear.Services.ServiceModels;

public sealed record RequestContext(string? UserId, string? IpAddress = null, string? CorrelationId = null);
public enum OperationOutcome { Success, Invalid, NotFound, Forbidden, Conflict, Busy }
public sealed record FieldError(string Field, string Message);
public sealed record OperationResult<T>(OperationOutcome Outcome, T? Value = default, string? Message = null,
    IReadOnlyList<FieldError>? Errors = null, bool Reload = false)
{
    public bool Succeeded => Outcome == OperationOutcome.Success;
    public static OperationResult<T> Ok(T? value = default, string? message = null) => new(OperationOutcome.Success, value, message);
    public static OperationResult<T> Invalid(string message, string field = "", bool reload = false) =>
        new(OperationOutcome.Invalid, Message: message, Errors: [new(field, message)], Reload: reload);
    public static OperationResult<T> Missing() => new(OperationOutcome.NotFound);
}

public static class WorkflowRecords
{
    public static bool MatchesVersion(string? encoded, byte[] current)
    {
        try { return encoded is not null && Convert.FromBase64String(encoded).SequenceEqual(current); }
        catch (FormatException) { return false; }
    }

    public static AuditEvent Audit(RequestContext actor, string action, string type, string id,
        DateTime now, string? before, string? after, string outcome = "Succeeded") => new()
        {
            ActorUserId = actor.UserId,
            Action = action,
            EntityType = type,
            EntityId = id,
            Outcome = outcome,
            BeforeJson = before,
            AfterJson = after,
            IpAddress = actor.IpAddress,
            CorrelationId = actor.CorrelationId,
            CreatedAtUtc = now
        };
}
