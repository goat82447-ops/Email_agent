using InboxAgent.Infrastructure;
using InboxAgent.Models;
using InboxAgent.Services;
using InboxAgent.Templates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// InboxAgent — reads Gmail, keeps only interview/placement emails (ignoring
// spam & promotions), builds a keyword-based summary and either emails it,
// shows it on a web dashboard, or both.
//
// Modes:
//   (default)      run as a daily scheduler (sends every morning at the
//                  configured time; also once on startup if enabled).
//   --web          start the dashboard website at http://localhost:5080 and
//                  also run the daily scheduler in the background.
//   --run-once     do a single scan now, email the digest, then exit (ideal
//                  for testing or for Windows Task Scheduler).

const string WebUrl = "http://localhost:5080";

var runOnce = HasFlag(args, "--run-once", "run-once");
var web = HasFlag(args, "--web", "web");

if (runOnce)
{
    var host = BuildHost(args);
    await host.Services.GetRequiredService<IDigestRunner>().RunOnceAsync();
    return;
}

if (web)
{
    var webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = ProjectPaths.ProjectRoot,
    });

    ConfigureConfiguration(webBuilder.Configuration, args);
    AddAgentServices(webBuilder.Services, webBuilder.Configuration);
    webBuilder.Services.AddHostedService<DigestSchedulerService>();

    var app = webBuilder.Build();
    app.MapGet("/", (IDigestStore store) =>
        Results.Content(DashboardPage.Render(store.Latest), "text/html"));
    app.MapPost("/refresh", async (IDigestRunner runner) =>
    {
        await runner.RunOnceAsync();
        return Results.Redirect("/");
    });
    app.MapPost("/delete", async (HttpRequest request, IGmailReader reader, IDigestStore store) =>
    {
        var form = await request.ReadFormAsync();
        if (ulong.TryParse(form["id"], out var id) && id != 0)
        {
            if (await reader.DeleteAsync(id))
            {
                store.RemoveInterview(id);
            }
        }
        return Results.Redirect("/");
    });

    // On Render (and most PaaS hosts) the platform injects the port to bind to
    // via the PORT env var and requires binding on 0.0.0.0. Locally we fall back
    // to a friendly localhost URL.
    var port = Environment.GetEnvironmentVariable("PORT");
    var url = string.IsNullOrWhiteSpace(port) ? WebUrl : $"http://0.0.0.0:{port}";

    Console.WriteLine($"Inbox Agent dashboard running at {url}");
    await app.RunAsync(url);
    return;
}

// Default: headless daily scheduler.
var schedulerBuilder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = ProjectPaths.ProjectRoot,
});
ConfigureConfiguration(schedulerBuilder.Configuration, args);
AddAgentServices(schedulerBuilder.Services, schedulerBuilder.Configuration);
schedulerBuilder.Services.AddHostedService<DigestSchedulerService>();
await schedulerBuilder.Build().RunAsync();

static bool HasFlag(string[] args, params string[] names) =>
    args.Any(a => names.Any(n => a.Equals(n, StringComparison.OrdinalIgnoreCase)));

static IHost BuildHost(string[] args)
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = ProjectPaths.ProjectRoot,
    });
    ConfigureConfiguration(builder.Configuration, args);
    AddAgentServices(builder.Services, builder.Configuration);
    return builder.Build();
}

static void ConfigureConfiguration(IConfigurationBuilder configuration, string[] args)
{
    configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

static void AddAgentServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<InboxOptions>().Bind(configuration.GetSection(InboxOptions.SectionName));
    services.AddOptions<DeliveryOptions>().Bind(configuration.GetSection(DeliveryOptions.SectionName));
    services.AddOptions<ScheduleOptions>().Bind(configuration.GetSection(ScheduleOptions.SectionName));
    services.AddOptions<ClassificationOptions>().Bind(configuration.GetSection(ClassificationOptions.SectionName));
    services.AddOptions<OpenAiOptions>().Bind(configuration.GetSection(OpenAiOptions.SectionName));

    services.AddSingleton<IDigestStore, InMemoryDigestStore>();
    services.AddSingleton<IGmailReader, GmailReader>();
    services.AddSingleton<IEmailClassifier, KeywordEmailClassifier>();
    services.AddSingleton<ISummarizer, EmailSummarizer>();
    services.AddSingleton<IDigestBuilder, DigestBuilder>();
    services.AddSingleton<IDigestSender, EmailDigestSender>();
    services.AddSingleton<IDigestRunner, DigestRunner>();
}

