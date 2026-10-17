using AiNexus.Host;
using AiNexus.Host.Commands;
using AiNexus.Features;
using AiNexus.Features.Monitoring;
using AiNexus.Features.Persistence;
using AiNexus.Platform;
using AiNexus.Platform.Configuration;
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
    NexusConfiguration.Load(builder, command.HostArguments);
    builder.AddPlatform();
    builder.AddFeatures();
    builder.Services.AddOpenApiContract();
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
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi().AllowAnonymous();
}
app.MapFeatures();

// 未知的 API 路徑不能回傳 SPA 的 index.html，否則前端會收到錯誤的內容。
app.Map("/api/{**path}", () => Results.NotFound()).RequireAuthorization();

// 預設的 FallbackToFile 會把最後一段含點號（.）的路徑當成遺失的靜態檔而略過。
// Gitea 程式庫路由常以 `team/my.repo` 或 `base...head` 這類路徑結尾，沒有實體檔案，所以要明確指定。
app.MapFallbackToFile("/repositories/{**path}", "index.html").AllowAnonymous();
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
