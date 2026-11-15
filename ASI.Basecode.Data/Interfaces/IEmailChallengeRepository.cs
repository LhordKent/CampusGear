using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IEmailChallengeRepository
    {
        Task<IReadOnlyList<EmailChallenge>> UnconsumedAsync(string userId, EmailChallengePurpose purpose, CancellationToken ct);
        Task<EmailChallenge?> LatestValidAsync(string userId, EmailChallengePurpose purpose, CancellationToken ct);
        void Add(EmailChallenge challenge);
        void DetachWrites();
    }
}
