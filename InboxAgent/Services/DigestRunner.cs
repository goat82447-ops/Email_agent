using InboxAgent.Models;
using Microsoft.Extensions.Logging;

namespace InboxAgent.Services;

/// <summary>Runs one full digest cycle: read → classify → summarize → send.</summary>
public interface IDigestRunner
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class DigestRunner : IDigestRunner
{
    private readonly IGmailReader _reader;
    private readonly IEmailClassifier _classifier;
    private readonly ISummarizer _summarizer;
    private readonly IDigestBuilder _builder;
    private readonly IDigestSender _sender;
    private readonly IDigestStore _store;
    private readonly ILogger<DigestRunner> _logger;

    public DigestRunner(
        IGmailReader reader,
        IEmailClassifier classifier,
        ISummarizer summarizer,
        IDigestBuilder builder,
        IDigestSender sender,
        IDigestStore store,
        ILogger<DigestRunner> logger)
    {
        _reader = reader;
        _classifier = classifier;
        _summarizer = summarizer;
        _builder = builder;
        _sender = sender;
        _store = store;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting inbox digest run...");

        var emails = await _reader.FetchRecentAsync(cancellationToken).ConfigureAwait(false);

        var interviewEmails = new List<EmailItem>();
        var ignored = 0;
        foreach (var email in emails)
        {
            if (_classifier.Classify(email) == EmailCategory.Interview)
            {
                interviewEmails.Add(email);
            }
            else
            {
                ignored++;
            }
        }

        _logger.LogInformation(
            "Classified {Total} email(s): {Kept} interview, {Ignored} ignored (spam/promotions/other).",
            emails.Count, interviewEmails.Count, ignored);

        // Summarize the interview emails (OpenAI if configured, keyword otherwise).
        var summarized = new List<SummarizedEmail>(interviewEmails.Count);
        foreach (var email in interviewEmails)
        {
            var summary = await _summarizer.SummarizeAsync(email, cancellationToken).ConfigureAwait(false);
            summarized.Add(new SummarizedEmail(email, summary));
        }

        // Save the snapshot so the web dashboard can display it.
        _store.Save(new DigestSnapshot(
            DateTimeOffset.Now, emails.Count, ignored, summarized));

        var digest = _builder.Build(summarized, emails.Count, ignored);
        await _sender.SendAsync(digest, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Inbox digest run complete.");
    }
}
