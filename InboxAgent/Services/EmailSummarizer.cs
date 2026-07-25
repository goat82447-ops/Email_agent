using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InboxAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InboxAgent.Services;

/// <summary>Produces a short summary for an interview email.</summary>
public interface ISummarizer
{
    Task<string> SummarizeAsync(EmailItem email, CancellationToken cancellationToken = default);
}

/// <summary>
/// Summarizes emails with OpenAI (ChatGPT) when an API key is configured, and
/// gracefully falls back to a keyword snippet when it is not, or if the API
/// call fails. This keeps the AI upgrade completely optional.
/// </summary>
public sealed class EmailSummarizer : ISummarizer, IDisposable
{
    private const int SnippetLength = 300;

    private readonly OpenAiOptions _options;
    private readonly ILogger<EmailSummarizer> _logger;
    private readonly HttpClient _http;

    public EmailSummarizer(IOptions<OpenAiOptions> options, ILogger<EmailSummarizer> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string> SummarizeAsync(EmailItem email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Snippet(email.BodyText);
        }

        try
        {
            return await SummarizeWithOpenAiAsync(email, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI summarization failed; using keyword snippet instead.");
            return Snippet(email.BodyText);
        }
    }

    private async Task<string> SummarizeWithOpenAiAsync(EmailItem email, CancellationToken cancellationToken)
    {
        // Trim the body so we don't send huge payloads / burn tokens.
        var body = email.BodyText.Length > 4000 ? email.BodyText[..4000] : email.BodyText;

        var payload = new
        {
            model = _options.Model,
            temperature = 0.2,
            max_tokens = 140,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You summarize interview and job-placement emails. Reply with 1-2 short sentences "
                        + "focused on the company/role, any action required, and any date/time or deadline. "
                        + "Be concise and plain. Do not add greetings or extra commentary.",
                },
                new
                {
                    role = "user",
                    content = $"Subject: {email.Subject}\nFrom: {email.FromName} <{email.FromAddress}>\n\n{body}",
                },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI returned {(int)response.StatusCode} {response.StatusCode}: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return string.IsNullOrWhiteSpace(content) ? Snippet(email.BodyText) : content.Trim();
    }

    private static string Snippet(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "(no readable text content)";
        }

        return body.Length <= SnippetLength ? body : body[..SnippetLength].TrimEnd() + "…";
    }

    public void Dispose() => _http.Dispose();
}
