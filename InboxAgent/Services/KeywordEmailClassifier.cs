using InboxAgent.Models;
using Microsoft.Extensions.Options;

namespace InboxAgent.Services;

/// <summary>Sorts an email into an <see cref="EmailCategory"/>.</summary>
public interface IEmailClassifier
{
    EmailCategory Classify(EmailItem email);
}

/// <summary>
/// Keyword-based classifier (no AI). An email is treated as interview-related
/// when it matches an interview keyword and is NOT clearly a promotion/spam
/// message. Everything else is ignored.
/// </summary>
public sealed class KeywordEmailClassifier : IEmailClassifier
{
    private readonly ClassificationOptions _options;

    public KeywordEmailClassifier(IOptions<ClassificationOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public EmailCategory Classify(EmailItem email)
    {
        var haystack = $"{email.Subject}\n{email.FromName}\n{email.FromAddress}\n{email.BodyText}";

        var isPromo = MatchesAny(haystack, _options.SpamPromoKeywords);
        var isInterview = MatchesAny(haystack, _options.InterviewKeywords);

        // Interview signal wins over a promo signal (e.g. "offer letter" in a
        // newsletter-styled email), because false negatives here are costly.
        if (isInterview)
        {
            return EmailCategory.Interview;
        }

        return isPromo ? EmailCategory.SpamOrPromotion : EmailCategory.Other;
    }

    private static bool MatchesAny(string text, IEnumerable<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) &&
                text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
