using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Manager
{
    public static class WorkflowRecords
    {
        public static bool MatchesVersion(string? encoded, byte[] current)
        {
            try
            {
                return encoded is not null && Convert.FromBase64String(encoded).SequenceEqual(current);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static AuditEvent Audit(
            RequestContext actor,
            string action,
            string type,
            string id,
            DateTime now,
            string? before,
            string? after,
            string outcome = "Succeeded"
        )
        {
            return new()
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
                CreatedAtUtc = now,
            };
        }
    }
}
