using AiNexus.Api;
using AiNexus.Api.Commands;
using AiNexus.Features;
using AiNexus.Features.Configuration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data;
using AiNexus.Platform.Diagnostics;
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
    builder.Services.AddOpenApi();
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
app.Lifetime.ApplicationStarted.Register(() => app.Logger.LogInformation(DiagnosticEvents.Started, "Service started."));
app.Lifetime.ApplicationStopping.Register(() => app.Logger.LogInformation(DiagnosticEvents.Stopping, "Service stopping."));
try { await app.RunAsync(); }
catch (Exception ex)
{
    await DiagnosticStartup.RecordAsync(ex, builder.Configuration, builder.Environment);
    throw;
}

public partial class Program;
