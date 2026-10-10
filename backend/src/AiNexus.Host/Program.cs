using AiNexus.Host;
using AiNexus.Host.Commands;
using AiNexus.Features;
using AiNexus.Features.Integrations;
using AiNexus.Features.Monitoring;
using AiNexus.Features.Persistence;
using AiNexus.Platform;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Health;
using AiNexus.Platform.Http;
using AiNexus.Platform.Security;
using AiNexus.Features.Identity.Sessions;

var command = HostCommand.Parse(args);
if (command.Help || command.Error is not null)
{
    if (command.Error is not null) Console.Error.WriteLine(command.Error);
    Console.WriteLine(HostCommand.Usage);
    return command.Error is null ? 0 : HostCommand.UsageError;
}

var builder = WebApplication.CreateBuilder(command.HostArguments);
WebApplication app;
try
{
    NexusConfiguration.Load(builder, command.HostArguments, IntegrationsSettings.SourceConnections);
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

if (command.Name is not null) return await command.RunAsync(app);

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
    await app.DisposeAsync();
    return 1;
}
app.Lifetime.ApplicationStarted.Register(() => LogStarted(app.Logger));
app.Lifetime.ApplicationStopping.Register(() => LogStopping(app.Logger));
try { await app.RunAsync(); }
catch (Exception ex)
{
    await DiagnosticStartup.RecordAsync(ex, builder.Configuration, builder.Environment);
    throw;
}
return 0;

public partial class Program
{
    [LoggerMessage(EventId = 5000, EventName = "service.started", Level = LogLevel.Information, Message = "Service started.")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(EventId = 5001, EventName = "service.stopping", Level = LogLevel.Information, Message = "Service stopping.")]
    private static partial void LogStopping(ILogger logger);
}
