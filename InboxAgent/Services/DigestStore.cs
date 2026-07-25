using InboxAgent.Models;

namespace InboxAgent.Services;

/// <summary>A snapshot of the most recent digest scan, shown on the dashboard.</summary>
public sealed record DigestSnapshot(
    DateTimeOffset GeneratedAt,
    int TotalScanned,
    int IgnoredCount,
    IReadOnlyList<SummarizedEmail> InterviewEmails);

/// <summary>Holds the latest digest snapshot so the web dashboard can display it.</summary>
public interface IDigestStore
{
    DigestSnapshot? Latest { get; }

    void Save(DigestSnapshot snapshot);

    /// <summary>Removes an interview email from the current snapshot (after it was deleted in Gmail).</summary>
    void RemoveInterview(ulong gmailMessageId);
}

/// <summary>Thread-safe in-memory store for the latest digest snapshot.</summary>
public sealed class InMemoryDigestStore : IDigestStore
{
    private volatile DigestSnapshot? _latest;

    public DigestSnapshot? Latest => _latest;

    public void Save(DigestSnapshot snapshot) => _latest = snapshot;

    public void RemoveInterview(ulong gmailMessageId)
    {
        var current = _latest;
        if (current is null || gmailMessageId == 0)
        {
            return;
        }

        var remaining = current.InterviewEmails
            .Where(e => e.Email.GmailMessageId != gmailMessageId)
            .ToList();

        if (remaining.Count != current.InterviewEmails.Count)
        {
            _latest = current with { InterviewEmails = remaining };
        }
    }
}
