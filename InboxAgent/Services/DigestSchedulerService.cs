using System.Globalization;
using InboxAgent.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InboxAgent.Services;

/// <summary>
/// Long-running scheduler that sends a digest every day at the configured local
/// time. Optionally sends one immediately on startup.
/// </summary>
public sealed class DigestSchedulerService : BackgroundService
{
    private readonly IDigestRunner _runner;
    private readonly ScheduleOptions _options;
    private readonly ILogger<DigestSchedulerService> _logger;

    public DigestSchedulerService(
        IDigestRunner runner,
        IOptions<ScheduleOptions> options,
        ILogger<DigestSchedulerService> logger)
    {
        _runner = runner;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.RunImmediatelyOnStart)
        {
            await SafeRunAsync(stoppingToken).ConfigureAwait(false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun();
            _logger.LogInformation(
                "Next digest scheduled for {NextRun:ddd dd MMM, HH:mm} (in {Hours:0.0}h).",
                DateTimeOffset.Now.Add(delay), delay.TotalHours);

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await SafeRunAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SafeRunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _runner.RunOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutting down — ignore.
        }
        catch (Exception ex)
        {
            // Never let a single failed run kill the scheduler.
            _logger.LogError(ex, "The scheduled digest run failed; will try again at the next scheduled time.");
        }
    }

    private TimeSpan TimeUntilNextRun()
    {
        var runTime = ParseRunTime(_options.DailyRunTime);
        var now = DateTimeOffset.Now;
        var next = new DateTimeOffset(
            now.Year, now.Month, now.Day, runTime.Hours, runTime.Minutes, 0, now.Offset);

        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }

    private TimeSpan ParseRunTime(string value)
    {
        if (TimeSpan.TryParseExact(value, "hh\\:mm", CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        _logger.LogWarning("Could not parse DailyRunTime '{Value}'; defaulting to 08:00.", value);
        return new TimeSpan(8, 0, 0);
    }
}
