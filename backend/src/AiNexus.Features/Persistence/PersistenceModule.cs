using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Events;
using AiNexus.Platform.Health;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Persistence;

public sealed class PersistenceModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddDbContext<NexusDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Nexus") ?? ""));
        // Constructor-injected into NexusDbContext, so it applies however the context options are registered.
        services.AddDomainEvents();
        services.AddDbContextSqlDatabase<NexusDbContext>();
        services.AddSqlDatabase<NexusMasterDatabase>();
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<DatabaseSchema>();
        services.AddScoped<SqlVectorCapabilities>();
        services.AddSingleton<StorageReadiness>();
        services.AddSingleton<DatabaseHealthCheck>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag]);
    }
}

public static class DatabaseStartup
{
    /// <summary>API calls other than sign-in fail fast with a safe 503 until the Nexus connection is configured.</summary>
    public static IApplicationBuilder UseStorageReadiness(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        if (http.Request.Path.StartsWithSegments("/api/v1") && !http.Request.Path.StartsWithSegments("/api/v1/auth"))
            http.RequestServices.GetRequiredService<StorageReadiness>().RequireConfigured();
        await next(http);
    });

    /// <summary>
    /// SQL Server migrations are checked (and optionally applied) before requests or hosted workers start.
    /// SQLite fixtures build the current model directly with EnsureCreated.
    /// </summary>
    /// <returns><see langword="false"/> when the schema is not usable; the caller stops the host.</returns>
    public static async Task<bool> EnsureDatabaseReadyAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<StorageReadiness>();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var migrate = app.Configuration.GetValue<bool>("Storage:ApplyMigrationsOnStartup");
        if (migrate) storage.RequireConfigured();
        if (!storage.Configured || !db.Database.IsSqlServer()) return true;
        try
        {
            if (migrate) await db.Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<DatabaseSchema>().RequireCurrentAsync(CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            await DiagnosticStartup.RecordAsync(ex, app.Configuration, app.Environment);
            Console.Error.WriteLine(ex is ApiException api ? api.Message : LocalDatabaseSettings.Diagnose(ex));
            return false;
        }
    }
}
