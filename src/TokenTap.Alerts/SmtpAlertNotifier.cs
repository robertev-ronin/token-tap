using System.Net;
using System.Net.Mail;
using TokenTap.Core.Models;

namespace TokenTap.Alerts;

public sealed class SmtpAlertNotifier : IAlertNotifier
{
    private readonly EmailAlertOptions _options;

    public SmtpAlertNotifier(EmailAlertOptions options)
    {
        _options = options;
    }

    public async Task NotifyAsync(AlertDecision decision, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _options.To.Count == 0)
        {
            return;
        }

        string? password = Environment.GetEnvironmentVariable(_options.PasswordSecretName);
        using MailMessage message = new()
        {
            From = new MailAddress(_options.From),
            Subject = $"Token-Tap {decision.Rule.Severity}: {decision.Rule.Name}",
            Body = decision.Message
        };

        foreach (string recipient in _options.To)
        {
            message.To.Add(recipient);
        }

        using SmtpClient client = new(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new NetworkCredential(_options.Username, password);
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(client.SendAsyncCancel);
        await client.SendMailAsync(message, cancellationToken);
    }
}
