using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace CampusGear.Services;

/// <summary>
/// An in-memory mailbox for local development. Messages are lost when the app restarts.
/// Register this only in Development; never register it for a deployed environment.
/// </summary>
public sealed class DevelopmentEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<DevelopmentEmailMessage> _messages = new();
    private readonly IWebHostEnvironment _environment;

    public DevelopmentEmailSender(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException("The development email sender cannot run outside Development.");
        }

        _messages.Enqueue(new DevelopmentEmailMessage(toEmail, subject, body, DateTime.UtcNow));
        while (_messages.Count > 30)
        {
            _messages.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<DevelopmentEmailMessage> GetRecent() => _messages.Reverse().ToArray();
}

public sealed record DevelopmentEmailMessage(string ToEmail, string Subject, string Body, DateTime SentAtUtc);
