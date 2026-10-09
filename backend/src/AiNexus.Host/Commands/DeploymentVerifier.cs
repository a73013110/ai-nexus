using AiNexus.Platform.Data;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Platform.Errors;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Host.Commands;

public static class DeploymentVerifier
{
    private static readonly System.Text.Json.JsonSerializerOptions Indented = new() { WriteIndented = true };

    // Read-only SQL checks, synthetic embedding/rerank requests and a transient attachment IO probe.
    public static async Task<bool> VerifyAsync(IServiceProvider services, IConfiguration config, IHostEnvironment environment, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        try
        {
            var inference = scope.ServiceProvider.GetRequiredService<IOptions<InferenceOptions>>().Value;
            var ad = scope.ServiceProvider.GetRequiredService<IOptions<AdAuthenticationOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<AiNexus.Features.Administration.AdministrationOptions>>().Value;
            var knowledge = scope.ServiceProvider.GetRequiredService<IOptions<AiNexus.Features.Knowledge.KnowledgeOptions>>().Value;
            var attachments = scope.ServiceProvider.GetRequiredService<IOptions<AiNexus.Features.Attachments.AttachmentOptions>>().Value;
            var diagnostics = scope.ServiceProvider.GetRequiredService<IOptions<AiNexus.Platform.Diagnostics.DiagnosticOptions>>().Value;
            var diagnosticPath = AiNexus.Platform.Diagnostics.DiagnosticJournal.Resolve(diagnostics.Directory, environment);
            Directory.CreateDirectory(diagnosticPath);
            var diagnosticProbe = Path.Combine(diagnosticPath, ".verify-" + Guid.NewGuid().ToString("N"));
            try { await File.WriteAllTextAsync(diagnosticProbe, "AiNexus diagnostic storage write probe", ct); }
            finally { if (File.Exists(diagnosticProbe)) File.Delete(diagnosticProbe); }
            _ = scope.ServiceProvider.GetRequiredService<IOptions<AiNexus.Features.Artifacts.ExportOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<AiNexus.Features.Integrations.IntegrationsOptions>>().Value;
            var search = scope.ServiceProvider.GetRequiredService<IOptions<AiNexus.Features.WebSearch.WebSearchOptions>>().Value;
            var gitea = scope.ServiceProvider.GetRequiredService<IOptions<AiNexus.Features.Repositories.GiteaOptions>>().Value;
            var sql = new SqlConnectionStringBuilder(config.GetConnectionString("Nexus"));
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var connected = await db.Database.CanConnectAsync(ct);
            if (connected) await scope.ServiceProvider.GetRequiredService<DatabaseSchema>().RequireCurrentAsync(ct);
            var pending = connected ? await scope.ServiceProvider.GetRequiredService<DatabaseSchema>().PendingMigrationsAsync(ct) : null;
            var providerConfigured = !inference.ProviderConcurrency.ContainsKey("google") || !string.IsNullOrWhiteSpace(inference.GoogleApiKey);
            var catalog = await scope.ServiceProvider.GetRequiredService<ModelCatalog>().GetAsync(ct);
            await scope.ServiceProvider.GetRequiredService<AiNexus.Features.Attachments.IAttachmentStorage>().VerifyAsync(ct);
            var retrieval = await scope.ServiceProvider.GetRequiredService<AiNexus.Features.Knowledge.Retrieval.RetrievalModelProbe>().CheckAsync(null, ct);
            var ready = connected && pending?.Count == 0 && catalog.ProviderAvailable && catalog.Models.Count > 0 && providerConfigured && (ad.Mode == "Windows" || ad.Configured) && retrieval.Embedding.Available == true && retrieval.Rerank.Available == true;
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
                environment = environment.EnvironmentName, configurationVersion = config.GetValue("ConfigurationVersion", 1),
                sqlConnected = connected, pendingMigrations = pending?.Count ?? -1, pendingMigrationIds = pending,
                sqlEncrypted = sql.Encrypt != SqlConnectionEncryptOption.Optional,
                trustsSqlCertificate = sql.TrustServerCertificate, authMode = ad.Mode, adConfigured = ad.Mode == "Windows" || ad.Configured,
                providers = catalog.Providers, providerConfigured, configuredModelCount = inference.Models.Count,
                providerAvailable = catalog.ProviderAvailable, availableModelCount = catalog.Models.Count, modelNotice = catalog.Notice,
                embeddingProvider = knowledge.EmbeddingProvider, embeddingDimensions = knowledge.Dimensions,
                embeddingAvailable = retrieval.Embedding.Available, embeddingNotice = retrieval.Embedding.Notice, rerankAvailable = retrieval.Rerank.Available, rerankNotice = retrieval.Rerank.Notice,
                webSearchEnabled = search.Enabled, giteaEnabled = gitea.Enabled,
                keyRingPath = config["DataProtection:KeyRingPath"], attachmentStoragePath = attachments.StoragePath, attachmentStorageWritable = true,
                diagnosticStoragePath = diagnosticPath, diagnosticStorageWritable = true, diagnosticCapacityBytes = diagnostics.MaxDiskBytes, diagnosticMaxSqlRows = diagnostics.MaxSqlRows, diagnosticOtlpEnabled = diagnostics.OtlpEnabled, ready
            }, Indented));
            return ready;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex is ApiException api ? api.Message : ex is Microsoft.Extensions.Options.OptionsValidationException ? "Deployment configuration validation failed. Check model, authentication, tool, connector and limit settings." : LocalDatabaseSettings.Diagnose(ex));
            return false;
        }
    }
}
