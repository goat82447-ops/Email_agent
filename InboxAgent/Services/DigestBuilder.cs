using System.Net;
using System.Text;
using InboxAgent.Models;

namespace InboxAgent.Services;

/// <summary>The rendered content of a daily digest email.</summary>
public sealed record Digest(string Subject, string HtmlBody, string TextBody);

/// <summary>Builds the digest email from the interview emails that were kept.</summary>
public interface IDigestBuilder
{
    Digest Build(IReadOnlyList<SummarizedEmail> interviewEmails, int totalScanned, int ignoredCount);
}

/// <summary>
/// Renders the morning summary. Each interview email becomes a short entry with
/// sender, subject, time received and its generated summary.
/// </summary>
public sealed class DigestBuilder : IDigestBuilder
{
    public Digest Build(IReadOnlyList<SummarizedEmail> interviewEmails, int totalScanned, int ignoredCount)
    {
        var today = DateTimeOffset.Now.ToString("dddd, dd MMM yyyy");
        var subject = interviewEmails.Count == 0
            ? $"Morning inbox digest — no interview emails ({today})"
            : $"Morning inbox digest — {interviewEmails.Count} interview email(s) ({today})";

        return new Digest(subject, BuildHtml(interviewEmails, totalScanned, ignoredCount, today),
            BuildText(interviewEmails, totalScanned, ignoredCount, today));
    }

    private static string BuildText(
        IReadOnlyList<SummarizedEmail> emails, int totalScanned, int ignoredCount, string today)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Morning inbox digest — {today}");
        sb.AppendLine($"Scanned {totalScanned} email(s); kept {emails.Count} interview-related; ignored {ignoredCount} spam/promotions/other.");
        sb.AppendLine(new string('-', 60));

        if (emails.Count == 0)
        {
            sb.AppendLine("No interview or placement emails found in the scan window.");
            return sb.ToString();
        }

        var index = 1;
        foreach (var item in emails)
        {
            sb.AppendLine();
            sb.AppendLine($"{index}. {item.Email.Subject}");
            sb.AppendLine($"   From: {item.Email.FromName} <{item.Email.FromAddress}>");
            sb.AppendLine($"   When: {item.Email.ReceivedLocal:ddd dd MMM, HH:mm}");
            sb.AppendLine($"   Summary: {item.Summary}");
            index++;
        }

        return sb.ToString();
    }

    private static string BuildHtml(
        IReadOnlyList<SummarizedEmail> emails, int totalScanned, int ignoredCount, string today)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#1f2933;max-width:640px;margin:auto;\">");
        sb.Append($"<h2 style=\"margin-bottom:4px;\">Morning inbox digest</h2>");
        sb.Append($"<p style=\"color:#616e7c;margin-top:0;\">{WebUtility.HtmlEncode(today)}</p>");
        sb.Append($"<p style=\"color:#616e7c;\">Scanned <b>{totalScanned}</b> email(s), kept <b>{emails.Count}</b> interview-related, ignored <b>{ignoredCount}</b> spam/promotions/other.</p>");

        if (emails.Count == 0)
        {
            sb.Append("<p>No interview or placement emails found in the scan window. 🎉</p>");
        }
        else
        {
            var index = 1;
            foreach (var item in emails)
            {
                sb.Append("<div style=\"border:1px solid #e4e7eb;border-radius:8px;padding:12px 16px;margin:12px 0;\">");
                sb.Append($"<div style=\"font-size:16px;font-weight:600;\">{index}. {WebUtility.HtmlEncode(item.Email.Subject)}</div>");
                sb.Append($"<div style=\"color:#616e7c;font-size:13px;margin:4px 0;\">From {WebUtility.HtmlEncode(item.Email.FromName)} &lt;{WebUtility.HtmlEncode(item.Email.FromAddress)}&gt; · {item.Email.ReceivedLocal:ddd dd MMM, HH:mm}</div>");
                sb.Append($"<div style=\"font-size:14px;line-height:1.5;\">{WebUtility.HtmlEncode(item.Summary)}</div>");
                sb.Append("</div>");
                index++;
            }
        }

        sb.Append("<p style=\"color:#9aa5b1;font-size:12px;\">Generated automatically by InboxAgent.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }
}
