using Microsoft.Extensions.Logging;

namespace CampusGear.Services;

/// <summary>
/// Development only: keeps a copy of every message in the in-memory mailbox, then also tries SMTP.
/// A failed SMTP send is logged rather than thrown because the copy is readable at /development/email.
/// </summary>
public sealed class DevelopmentMirrorEmailSender : IEmailSender
{
    private readonly DevelopmentEmailSender _mailbox;
    private readonly SmtpEmailSender _smtp;
    private readonly ILogger<DevelopmentMirrorEmailSender> _logger;

    public DevelopmentMirrorEmailSender(DevelopmentEmailSender mailbox, SmtpEmailSender smtp,
        ILogger<DevelopmentMirrorEmailSender> logger)
    {
        _mailbox = mailbox;
        _smtp = smtp;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        await _mailbox.SendAsync(toEmail, subject, body, cancellationToken);
        try
        {
            await _smtp.SendAsync(toEmail, subject, body, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "SMTP delivery failed; the message is available at /development/email.");
        }
    }
}
