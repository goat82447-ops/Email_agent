using InboxAgent.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InboxAgent.Services;

/// <summary>Sends the rendered digest email.</summary>
public interface IDigestSender
{
    Task SendAsync(Digest digest, CancellationToken cancellationToken = default);
}

/// <summary>Sends the digest via Gmail SMTP (STARTTLS) using MailKit.</summary>
public sealed class EmailDigestSender : IDigestSender
{
    private readonly DeliveryOptions _options;
    private readonly ILogger<EmailDigestSender> _logger;

    public EmailDigestSender(IOptions<DeliveryOptions> options, ILogger<EmailDigestSender> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendAsync(Digest digest, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SenderEmail) ||
            string.IsNullOrWhiteSpace(_options.SenderAppPassword) ||
            string.IsNullOrWhiteSpace(_options.RecipientEmail))
        {
            _logger.LogWarning(
                "Delivery settings are incomplete; digest was not sent. Configure them in appsettings.Local.json.");
            return;
        }

        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.SenderEmail));
        message.To.Add(MailboxAddress.Parse(_options.RecipientEmail));
        message.Subject = digest.Subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = digest.HtmlBody,
            TextBody = digest.TextBody,
        }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(
                _options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, cancellationToken)
                .ConfigureAwait(false);
            await client.AuthenticateAsync(
                _options.SenderEmail, _options.SenderAppPassword, cancellationToken).ConfigureAwait(false);
            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Digest email sent to {Recipient}.", _options.RecipientEmail);
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex, "SMTP authentication failed. Verify the sender App Password.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while sending the digest email.");
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
