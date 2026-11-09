using CampusGear.Models;
namespace CampusGear.Services;
public interface IEmailChallengeService
{
    Task<ChallengeIssueResult> IssueAsync(ApplicationUser user, EmailChallengePurpose purpose, CancellationToken ct = default);
    Task<ChallengeVerifyStatus> VerifyAsync(string userId, EmailChallengePurpose purpose, string? code, CancellationToken ct = default);
}
