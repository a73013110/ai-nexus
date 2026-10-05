using AiNexus.Database;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.BuildingBlocks;

public static class DeploymentVerifier
{
    // Read-only SQL and model-list checks. No hosted workers, AD logins or generation requests.
    public static async Task<bool> VerifyAsync(IServiceProvider services, IConfiguration config, IHostEnvironment environment, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        try
        {
            var inference = scope.ServiceProvider.GetRequiredService<IOptions<InferenceOptions>>().Value;
            var ad = scope.ServiceProvider.GetRequiredService<IOptions<AdAuthenticationOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Administration.AdministrationOptions>>().Value;
            var knowledge = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Knowledge.KnowledgeOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Attachments.AttachmentOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Artifacts.ExportOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Integrations.IntegrationsOptions>>().Value;
            var search = scope.ServiceProvider.GetRequiredService<IOptions<Modules.WebSearch.WebSearchOptions>>().Value;
            var gitea = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Repositories.GiteaOptions>>().Value;
            var sql = new SqlConnectionStringBuilder(config.GetConnectionString("Nexus"));
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var connected = await db.Database.CanConnectAsync(ct);
            var pending = connected ? await scope.ServiceProvider.GetRequiredService<DatabaseSchema>().PendingMigrationsAsync(ct) : null;
            var providerConfigured = inference.Provider != "google" || !string.IsNullOrWhiteSpace(inference.GoogleApiKey);
            var catalog = await scope.ServiceProvider.GetRequiredService<ModelCatalog>().GetAsync(ct);
            var ready = connected && pending?.Count == 0 && catalog.ProviderAvailable && catalog.Models.Count > 0 && providerConfigured && (ad.Mode == "Windows" || ad.Configured);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
                environment = environment.EnvironmentName, configurationVersion = config.GetValue("ConfigurationVersion", 1),
                sqlConnected = connected, pendingMigrations = pending?.Count ?? -1, pendingMigrationIds = pending,
                sqlEncrypted = sql.Encrypt != SqlConnectionEncryptOption.Optional,
                trustsSqlCertificate = sql.TrustServerCertificate, authMode = ad.Mode, adConfigured = ad.Mode == "Windows" || ad.Configured,
                provider = inference.Provider, providerConfigured, configuredModelCount = inference.Models.Count,
                providerAvailable = catalog.ProviderAvailable, availableModelCount = catalog.Models.Count, modelNotice = catalog.Notice,
                embeddingProvider = knowledge.EmbeddingProvider, embeddingDimensions = knowledge.Dimensions,
                webSearchEnabled = search.Enabled, giteaEnabled = gitea.Enabled,
                keyRingPath = config["DataProtection:KeyRingPath"], ready
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return ready;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex is Microsoft.Extensions.Options.OptionsValidationException ? "Deployment configuration validation failed. Check model, authentication, tool, connector and limit settings." : LocalDatabaseSettings.Diagnose(ex));
            return false;
        }
    }
}
