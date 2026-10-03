namespace CampusGear.Services;

/// <summary>Sends a transactional email. Production must register a real implementation.</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}
