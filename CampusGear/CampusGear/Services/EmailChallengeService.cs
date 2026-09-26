using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Services;

public enum ChallengeIssueStatus { Sent, Cooldown, DeliveryUnavailable }
public sealed record ChallengeIssueResult(ChallengeIssueStatus Status, TimeSpan? RetryAfter = null);

public enum ChallengeVerifyStatus { Verified, Invalid, Expired, TooManyAttempts }

/// <summary>Creates and consumes one-time, email-delivered challenges.</summary>
public sealed class EmailChallengeService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private const int MaxAttempts = 5;

    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IDataProtector _protector;

    public EmailChallengeService(
        ApplicationDbContext db,
        IEmailSender emailSender,
        IDataProtectionProvider dataProtectionProvider)
    {
        _db = db;
        _emailSender = emailSender;
        _protector = dataProtectionProvider.CreateProtector("CampusGear.EmailChallenge.v1");
    }

    public async Task<ChallengeIssueResult> IssueAsync(
        ApplicationUser user,
        EmailChallengePurpose purpose,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return new ChallengeIssueResult(ChallengeIssueStatus.DeliveryUnavailable);
        }

        var now = DateTime.UtcNow;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString("D6", CultureInfo.InvariantCulture);
        EmailChallenge challenge;

        // The serializable transaction prevents two concurrent resends from creating valid codes.
        await using (var transaction = await _db.Database.BeginTransactionAsync(
                         IsolationLevel.Serializable, cancellationToken))
        {
            var previous = await _db.EmailChallenges
                .Where(x => x.UserId == user.Id && x.Purpose == purpose &&
                            x.ConsumedAtUtc == null)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var latest = previous.FirstOrDefault();
            if (latest is not null && latest.LastSentAtUtc.Add(ResendCooldown) > now)
            {
                return new ChallengeIssueResult(
                    ChallengeIssueStatus.Cooldown,
                    latest.LastSentAtUtc.Add(ResendCooldown) - now);
            }

            foreach (var older in previous.Where(x => x.InvalidatedAtUtc == null))
            {
                older.InvalidatedAtUtc = now;
            }

            challenge = new EmailChallenge
            {
                UserId = user.Id,
                Email = user.Email,
                Purpose = purpose,
                CreatedAtUtc = now,
                LastSentAtUtc = now,
                ExpiresAtUtc = now.Add(Lifetime),
                MaxAttempts = MaxAttempts
            };
            challenge.CodeHash = _protector.Protect($"{challenge.Id:N}:{code}");
            _db.EmailChallenges.Add(challenge);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        try
        {
            await _emailSender.SendAsync(
                user.Email,
                SubjectFor(purpose),
                $"Your CampusGear verification code is {code}. It expires in 10 minutes. " +
                "If you did not request this code, you can ignore this email.",
                cancellationToken);
            return new ChallengeIssueResult(ChallengeIssueStatus.Sent);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // An unsent challenge must not keep the user in the resend cooldown.
            challenge.InvalidatedAtUtc = DateTime.UtcNow;
            challenge.LastSentAtUtc = challenge.InvalidatedAtUtc.Value.Subtract(ResendCooldown);
            await _db.SaveChangesAsync(cancellationToken);
            return new ChallengeIssueResult(ChallengeIssueStatus.DeliveryUnavailable);
        }
    }

    public async Task<ChallengeVerifyStatus> VerifyAsync(
        string userId,
        EmailChallengePurpose purpose,
        string? submittedCode,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var challenge = await _db.EmailChallenges
            .Where(x => x.UserId == userId && x.Purpose == purpose &&
                        x.ConsumedAtUtc == null && x.InvalidatedAtUtc == null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge is null)
        {
            return ChallengeVerifyStatus.Invalid;
        }

        if (challenge.ExpiresAtUtc <= now)
        {
            challenge.InvalidatedAtUtc = now;
            await SaveChallengeAsync(cancellationToken);
            return ChallengeVerifyStatus.Expired;
        }

        if (challenge.AttemptCount >= challenge.MaxAttempts)
        {
            challenge.InvalidatedAtUtc = now;
            await SaveChallengeAsync(cancellationToken);
            return ChallengeVerifyStatus.TooManyAttempts;
        }

        var expected = string.Empty;
        try
        {
            expected = _protector.Unprotect(challenge.CodeHash);
        }
        catch (CryptographicException)
        {
            // A rotated or unavailable protection key invalidates only this short-lived code.
        }

        var received = $"{challenge.Id:N}:{submittedCode?.Trim()}";
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var receivedBytes = Encoding.UTF8.GetBytes(received);
        var matches = expectedBytes.Length == receivedBytes.Length &&
                      CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);

        challenge.AttemptCount++;
        if (matches)
        {
            challenge.ConsumedAtUtc = now;
        }
        else if (challenge.AttemptCount >= challenge.MaxAttempts)
        {
            challenge.InvalidatedAtUtc = now;
        }

        // The SQL rowversion ensures a code cannot be consumed twice under parallel posts.
        if (!await SaveChallengeAsync(cancellationToken))
        {
            return ChallengeVerifyStatus.Invalid;
        }

        if (matches) return ChallengeVerifyStatus.Verified;
        return challenge.AttemptCount >= challenge.MaxAttempts
            ? ChallengeVerifyStatus.TooManyAttempts
            : ChallengeVerifyStatus.Invalid;
    }

    private async Task<bool> SaveChallengeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            foreach (var entry in exception.Entries) entry.State = EntityState.Detached;
            return false;
        }
    }

    private static string SubjectFor(EmailChallengePurpose purpose) => purpose switch
    {
        EmailChallengePurpose.SignupVerification => "Verify your CampusGear email",
        EmailChallengePurpose.AdministratorLogin => "CampusGear administrator sign-in code",
        EmailChallengePurpose.PasswordReset => "Reset your CampusGear password",
        _ => "CampusGear verification code"
    };
}
