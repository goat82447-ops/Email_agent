namespace InboxAgent.Models;

/// <summary>The bucket an email is sorted into by the classifier.</summary>
public enum EmailCategory
{
    /// <summary>Interview / placement / hiring related — kept for the digest.</summary>
    Interview,

    /// <summary>Spam or promotional / marketing — ignored.</summary>
    SpamOrPromotion,

    /// <summary>Anything else — ignored (not interview related).</summary>
    Other,
}

/// <summary>A single email fetched from the mailbox.</summary>
public sealed record EmailItem(
    string FromName,
    string FromAddress,
    string Subject,
    DateTimeOffset ReceivedLocal,
    string BodyText,
    ulong GmailMessageId = 0);

/// <summary>An email together with the category the classifier assigned it.</summary>
public sealed record ClassifiedEmail(EmailItem Email, EmailCategory Category);

/// <summary>An interview email together with its generated summary text.</summary>
public sealed record SummarizedEmail(EmailItem Email, string Summary);
