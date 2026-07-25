using System.ComponentModel.DataAnnotations;

namespace InboxAgent.Models;

/// <summary>Settings for reading the mailbox over IMAP.</summary>
public sealed class InboxOptions
{
    public const string SectionName = "Inbox";

    [Required]
    public string ImapHost { get; set; } = "imap.gmail.com";

    [Range(1, 65535)]
    public int ImapPort { get; set; } = 993;

    [Required]
    public string EmailAddress { get; set; } = string.Empty;

    [Required]
    public string AppPassword { get; set; } = string.Empty;

    public string Folder { get; set; } = "INBOX";

    /// <summary>How many hours back to scan for new mail.</summary>
    [Range(1, 720)]
    public int LookbackHours { get; set; } = 24;

    /// <summary>Safety cap on how many messages to download per run.</summary>
    [Range(1, 500)]
    public int MaxEmails { get; set; } = 50;
}

/// <summary>Settings for sending the daily digest email.</summary>
public sealed class DeliveryOptions
{
    public const string SectionName = "Delivery";

    [Required]
    public string SmtpHost { get; set; } = "smtp.gmail.com";

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    [Required]
    public string SenderEmail { get; set; } = string.Empty;

    [Required]
    public string SenderAppPassword { get; set; } = string.Empty;

    [Required]
    public string RecipientEmail { get; set; } = string.Empty;
}

/// <summary>Settings for the daily schedule.</summary>
public sealed class ScheduleOptions
{
    public const string SectionName = "Schedule";

    /// <summary>Local time of day (24-hour "HH:mm") to send the digest.</summary>
    public string DailyRunTime { get; set; } = "08:00";

    /// <summary>Send one digest immediately when the agent starts.</summary>
    public bool RunImmediatelyOnStart { get; set; } = true;
}

/// <summary>Keyword rules for classifying emails.</summary>
public sealed class ClassificationOptions
{
    public const string SectionName = "Classification";

    public List<string> InterviewKeywords { get; set; } = new();

    public List<string> SpamPromoKeywords { get; set; } = new();
}

/// <summary>
/// Optional OpenAI (ChatGPT) settings. When <see cref="ApiKey"/> is empty the
/// agent falls back to a simple keyword-based summary.
/// </summary>
public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    /// <summary>OpenAI API key. Leave empty to disable AI summaries.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Chat model to use (e.g. gpt-4o-mini).</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Base API address.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
}
