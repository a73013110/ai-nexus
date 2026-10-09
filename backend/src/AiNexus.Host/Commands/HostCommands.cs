using AiNexus.Features.Persistence;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;

namespace AiNexus.Host.Commands;

/// <summary>
/// Operational one-shot modes (<c>--InitializeDatabase true</c>, …) used by the deployment scripts. Each runs against the
/// fully built host, sets the process exit code and disposes the host instead of serving HTTP.
/// </summary>
public static class HostCommands
{
    private delegate Task<bool> Command(WebApplication app);

    private static readonly (string Switch, Command Run)[] Commands =
    [
        ("VerifyDeployment", app => DeploymentVerifier.VerifyAsync(app.Services, app.Configuration, app.Environment, CancellationToken.None)),
        ("InitializeDatabase", app => InScopeAsync(app, async services =>
        {
            await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
            await DatabaseDescriptionVerifier.VerifyAsync(services.GetRequiredService<NexusDbContext>(), CancellationToken.None);
            Console.WriteLine("AiNexus 資料庫與 migrations 初始化完成。");
        })),
        ("VerifyDatabaseDescriptions", app => InScopeAsync(app, services =>
            DatabaseDescriptionVerifier.VerifyAsync(services.GetRequiredService<NexusDbContext>(), CancellationToken.None))),
        ("VerifySqlCapabilities", app => InScopeAsync(app, services =>
            services.GetRequiredService<SqlVectorCapabilities>().VerifyAsync(app.Configuration["VerificationOutput"] ?? "sql-capabilities.json", CancellationToken.None))),
        ("VerifyConnections", async app =>
        {
            using var scope = app.Services.CreateScope();
            return await ConnectionVerifier.VerifyAsync(scope.ServiceProvider, app.Configuration, app.Environment.ContentRootPath, CancellationToken.None);
        }),
    ];

    /// <returns><see langword="true"/> when a command ran; the host has been disposed and the process should exit.</returns>
    public static async Task<bool> TryRunAsync(WebApplication app)
    {
        foreach (var (name, run) in Commands)
        {
            if (!app.Configuration.GetValue<bool>(name)) continue;
            if (!await run(app)) Environment.ExitCode = 1;
            await app.DisposeAsync();
            return true;
        }
        return false;
    }

    private static async Task<bool> InScopeAsync(WebApplication app, Func<IServiceProvider, Task> work)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            await work(scope.ServiceProvider);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex is ApiException api ? api.Message : LocalDatabaseSettings.Diagnose(ex));
            return false;
        }
    }
}
