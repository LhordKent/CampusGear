using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Microsoft.EntityFrameworkCore;

// EMAIL CHALLENGE REPOSITORY  -  stores the one-time email codes (admin login, password reset).
namespace ASI.Basecode.Data.Repositories
{
    public sealed class EmailChallengeRepository : BaseRepository, IEmailChallengeRepository
    {
        public EmailChallengeRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db)
            : base(unitOfWork, db) { }

        public async Task<IReadOnlyList<EmailChallenge>> UnconsumedAsync(string userId, EmailChallengePurpose purpose, CancellationToken ct)
        {
            return await Context
                .EmailChallenges.Where(x => x.UserId == userId && x.Purpose == purpose && x.ConsumedAtUtc == null)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(ct);
        }

        // WBS 4.00 | Select the newest unconsumed, non-invalidated code; the service checks expiry and attempts.
        public Task<EmailChallenge?> LatestValidAsync(string userId, EmailChallengePurpose purpose, CancellationToken ct)
        {
            return Context
                .EmailChallenges.Where(x =>
                    x.UserId == userId && x.Purpose == purpose && x.ConsumedAtUtc == null && x.InvalidatedAtUtc == null
                )
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
        }

        public void Add(EmailChallenge challenge) => Context.EmailChallenges.Add(challenge);

        public void DetachWrites()
        {
            foreach (var entry in Context.ChangeTracker.Entries<EmailChallenge>().Where(x => x.State == EntityState.Modified).ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
