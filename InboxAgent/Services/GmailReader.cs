using InboxAgent.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InboxAgent.Services;

/// <summary>Reads recent emails from a mailbox over IMAP.</summary>
public interface IGmailReader
{
    Task<IReadOnlyList<EmailItem>> FetchRecentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the message with the given Gmail message id to the Gmail Trash
    /// (i.e. deletes it). Returns true if a message was moved.
    /// </summary>
    Task<bool> DeleteAsync(ulong gmailMessageId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches recent messages from Gmail (or any IMAP server) using MailKit,
/// authenticating with an App Password over an implicit SSL connection.
/// </summary>
public sealed class GmailReader : IGmailReader
{
    private readonly InboxOptions _options;
    private readonly ILogger<GmailReader> _logger;

    public GmailReader(IOptions<InboxOptions> options, ILogger<GmailReader> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<EmailItem>> FetchRecentAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.EmailAddress) ||
            string.IsNullOrWhiteSpace(_options.AppPassword))
        {
            _logger.LogWarning(
                "Inbox EmailAddress/AppPassword are not configured; skipping mailbox read. " +
                "Set them in appsettings.Local.json.");
            return Array.Empty<EmailItem>();
        }

        using var client = new ImapClient();
        var results = new List<EmailItem>();

        try
        {
            _logger.LogInformation(
                "Connecting to IMAP {Host}:{Port}...", _options.ImapHost, _options.ImapPort);
            await client.ConnectAsync(
                _options.ImapHost, _options.ImapPort, SecureSocketOptions.SslOnConnect, cancellationToken)
                .ConfigureAwait(false);

            await client.AuthenticateAsync(_options.EmailAddress, _options.AppPassword, cancellationToken)
                .ConfigureAwait(false);

            IMailFolder? folder = string.Equals(_options.Folder, "INBOX", StringComparison.OrdinalIgnoreCase)
                ? client.Inbox
                : await client.GetFolderAsync(_options.Folder, cancellationToken).ConfigureAwait(false);

            if (folder is null)
            {
                _logger.LogWarning("Mailbox folder '{Folder}' was not found.", _options.Folder);
                return Array.Empty<EmailItem>();
            }

            await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);

            var since = DateTimeOffset.Now.AddHours(-_options.LookbackHours);
            // IMAP date search is day-granular, so search from the calendar day of
            // the cutoff and filter to the exact instant in code.
            var uids = await folder.SearchAsync(
                SearchQuery.DeliveredAfter(since.Date.AddDays(-1)), cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("IMAP search returned {Count} candidate message(s).", uids.Count);

            // Fetch the stable Gmail message id for each uid so the dashboard can
            // reference a message later (e.g. to delete it). UIDs can change, but
            // the X-GM-MSGID is stable for the account.
            var gmailIdByUid = new Dictionary<UniqueId, ulong>();
            if (uids.Count > 0)
            {
                var summaries = await folder.FetchAsync(
                    uids, MessageSummaryItems.GMailMessageId, cancellationToken).ConfigureAwait(false);
                foreach (var summary in summaries)
                {
                    gmailIdByUid[summary.UniqueId] = summary.GMailMessageId ?? 0UL;
                }
            }

            // Newest first, capped by MaxEmails.
            foreach (var uid in uids.Reverse())
            {
                if (results.Count >= _options.MaxEmails)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var message = await folder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
                var received = message.Date == default ? DateTimeOffset.Now : message.Date.ToLocalTime();

                if (received < since)
                {
                    continue;
                }

                var from = message.From.Mailboxes.FirstOrDefault();
                var body = message.TextBody
                           ?? StripHtml(message.HtmlBody)
                           ?? string.Empty;

                results.Add(new EmailItem(
                    FromName: from?.Name ?? from?.Address ?? "(unknown sender)",
                    FromAddress: from?.Address ?? string.Empty,
                    Subject: message.Subject ?? "(no subject)",
                    ReceivedLocal: received,
                    BodyText: Normalize(body),
                    GmailMessageId: gmailIdByUid.TryGetValue(uid, out var gid) ? gid : 0UL));
            }

            _logger.LogInformation(
                "Fetched {Count} email(s) from the last {Hours}h.", results.Count, _options.LookbackHours);
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex,
                "IMAP authentication failed. Verify the Gmail App Password and that IMAP is enabled.");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Mailbox read was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while reading the mailbox.");
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
            }
        }

        return results;
    }

    public async Task<bool> DeleteAsync(ulong gmailMessageId, CancellationToken cancellationToken = default)
    {
        if (gmailMessageId == 0 ||
            string.IsNullOrWhiteSpace(_options.EmailAddress) ||
            string.IsNullOrWhiteSpace(_options.AppPassword))
        {
            _logger.LogWarning("Cannot delete: message id or mailbox credentials are missing.");
            return false;
        }

        using var client = new ImapClient();
        try
        {
            await client.ConnectAsync(
                _options.ImapHost, _options.ImapPort, SecureSocketOptions.SslOnConnect, cancellationToken)
                .ConfigureAwait(false);
            await client.AuthenticateAsync(_options.EmailAddress, _options.AppPassword, cancellationToken)
                .ConfigureAwait(false);

            IMailFolder? folder = string.Equals(_options.Folder, "INBOX", StringComparison.OrdinalIgnoreCase)
                ? client.Inbox
                : await client.GetFolderAsync(_options.Folder, cancellationToken).ConfigureAwait(false);

            if (folder is null)
            {
                _logger.LogWarning("Mailbox folder '{Folder}' was not found.", _options.Folder);
                return false;
            }

            // Read-write so we can move the message out of the inbox.
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);

            var uids = await folder.SearchAsync(
                SearchQuery.GMailMessageId(gmailMessageId), cancellationToken).ConfigureAwait(false);
            if (uids.Count == 0)
            {
                _logger.LogWarning("Message {Id} was not found in '{Folder}'.", gmailMessageId, _options.Folder);
                return false;
            }

            var trash = client.GetFolder(SpecialFolder.Trash);
            if (trash is null)
            {
                _logger.LogWarning("Gmail Trash folder was not found; cannot delete message {Id}.", gmailMessageId);
                return false;
            }

            await folder.MoveToAsync(uids, trash, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Moved message {Id} to Trash.", gmailMessageId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move message {Id} to Trash.", gmailMessageId);
            return false;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(text);
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }
}
