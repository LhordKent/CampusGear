using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IEmailChallengeService
    {
        Task<ChallengeIssueResult> IssueAsync(User user, EmailChallengePurpose purpose, CancellationToken ct = default);
        Task<ChallengeVerifyStatus> VerifyAsync(string userId, EmailChallengePurpose purpose, string? code, CancellationToken ct = default);
    }
}
