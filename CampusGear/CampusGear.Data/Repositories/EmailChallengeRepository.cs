using CampusGear.Data.Interfaces;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
namespace CampusGear.Data.Repositories;
public sealed class EmailChallengeRepository(ApplicationDbContext db) : IEmailChallengeRepository
{
    public async Task<IReadOnlyList<EmailChallenge>> UnconsumedAsync(string userId, EmailChallengePurpose purpose, CancellationToken ct) =>
        await db.EmailChallenges.Where(x => x.UserId == userId && x.Purpose == purpose && x.ConsumedAtUtc == null)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    public Task<EmailChallenge?> LatestValidAsync(string userId, EmailChallengePurpose purpose, CancellationToken ct) =>
        db.EmailChallenges.Where(x => x.UserId == userId && x.Purpose == purpose && x.ConsumedAtUtc == null && x.InvalidatedAtUtc == null)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
    public void Add(EmailChallenge challenge) => db.EmailChallenges.Add(challenge);
    public void DetachWrites()
    {
        foreach (var entry in db.ChangeTracker.Entries<EmailChallenge>().Where(x => x.State == EntityState.Modified).ToList())
            entry.State = EntityState.Detached;
    }
}
