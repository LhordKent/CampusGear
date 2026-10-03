using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace CampusGear.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(IConfiguration configuration) => _configuration = configuration;

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Email:Smtp:Host"];
        var from = _configuration["Email:Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Email:Smtp:Host and Email:Smtp:From must be configured before sending email.");

        using var message = new MailMessage(from, toEmail, subject, body) { IsBodyHtml = false };
        using var client = new SmtpClient(host, _configuration.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = _configuration.GetValue("Email:Smtp:EnableSsl", true)
        };
        var username = _configuration["Email:Smtp:Username"];
        var password = _configuration["Email:Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        // SmtpClient.Timeout does not bound asynchronous sends.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        await client.SendMailAsync(message, timeout.Token);
    }
}
