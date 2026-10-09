using AiNexus.Host;
using AiNexus.Host.Commands;
using AiNexus.Features;
using AiNexus.Features.Configuration;
using AiNexus.Features.Identity;
using AiNexus.Features.Monitoring;
using AiNexus.Features.Persistence;
using AiNexus.Platform;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Health;
using AiNexus.Platform.Http;
using AiNexus.Platform.Security;

var builder = WebApplication.CreateBuilder(args);
WebApplication app;
try
{
    NexusConfiguration.Load(builder, args, NexusSettings.SourceConnections);
    LocalDatabaseSettings.Apply(builder.Configuration);
    builder.AddPlatform();
    builder.AddFeatures();
    builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
    {
        document.Servers = [new() { Url = "/" }];
        return Task.CompletedTask;
    }));
    builder.ConfigureServerLimits();
    app = builder.Build();
    FeatureModules.VerifyStartup(app.Services);
}
catch (Exception ex)
{
    await DiagnosticStartup.RecordAsync(ex, builder.Configuration, builder.Environment);
    throw;
}

if (await HostCommands.TryRunAsync(app)) return;

app.UseRuntimeTraffic();
app.UseMiddleware<DiagnosticRequestMiddleware>();
app.UseStatusCodePages();
app.UseTransportSecurity();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRequestBodyLimits();
app.UseAuthentication();
app.UseIdentityConsistency();
app.UseAuthorization();
app.UseRateLimiter();
app.UseStorageReadiness();
app.UseCsrfProtection();

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.MapReadinessCheck();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) app.MapOpenApi().AllowAnonymous();
app.MapFeatures();
// Unknown API paths must never return the SPA's HTML document.
app.Map("/api/{**path}", () => Results.NotFound()).RequireAuthorization();
app.MapFallbackToFile("index.html").AllowAnonymous();

if (!await app.EnsureDatabaseReadyAsync())
{
    Environment.ExitCode = 1;
    await app.DisposeAsync();
    return;
}
app.Lifetime.ApplicationStarted.Register(() => LogStarted(app.Logger));
app.Lifetime.ApplicationStopping.Register(() => LogStopping(app.Logger));
try { await app.RunAsync(); }
catch (Exception ex)
{
    await DiagnosticStartup.RecordAsync(ex, builder.Configuration, builder.Environment);
    throw;
}

public partial class Program
{
    [LoggerMessage(EventId = 5000, EventName = "service.started", Level = LogLevel.Information, Message = "Service started.")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(EventId = 5001, EventName = "service.stopping", Level = LogLevel.Information, Message = "Service stopping.")]
    private static partial void LogStopping(ILogger logger);
}
