using AiNexus.Database;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.BuildingBlocks;

public static class DeploymentVerifier
{
    // Read-only SQL checks. No hosted workers, AD login attempts or AI calls are started.
    public static async Task<bool> VerifyAsync(IServiceProvider services, IConfiguration config, IHostEnvironment environment, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        try
        {
            var inference = scope.ServiceProvider.GetRequiredService<IOptions<InferenceOptions>>().Value;
            var ad = scope.ServiceProvider.GetRequiredService<IOptions<AdAuthenticationOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Administration.AdministrationOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Knowledge.KnowledgeOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Attachments.AttachmentOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Artifacts.ExportOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<Modules.Integrations.IntegrationsOptions>>().Value;
            var sql = new SqlConnectionStringBuilder(config.GetConnectionString("Nexus"));
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var connected = await db.Database.CanConnectAsync(ct);
            var pending = connected ? (await db.Database.GetPendingMigrationsAsync(ct)).Count() : -1;
            var providerConfigured = inference.Provider != "google" || !string.IsNullOrWhiteSpace(inference.GoogleApiKey);
            var ready = connected && pending == 0 && inference.Models.Count > 0 && providerConfigured && (ad.Mode == "Windows" || ad.Configured);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
                environment = environment.EnvironmentName, configurationVersion = config.GetValue("ConfigurationVersion", 1),
                sqlConnected = connected, pendingMigrations = pending, sqlEncrypted = sql.Encrypt != SqlConnectionEncryptOption.Optional,
                trustsSqlCertificate = sql.TrustServerCertificate, authMode = ad.Mode, adConfigured = ad.Mode == "Windows" || ad.Configured,
                provider = inference.Provider, providerConfigured, configuredModelCount = inference.Models.Count,
                keyRingPath = config["DataProtection:KeyRingPath"], ready
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return ready;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex is Microsoft.Extensions.Options.OptionsValidationException ? "Deployment configuration validation failed. Check model, authentication, attachment and limit settings." : LocalDatabaseSettings.Diagnose(ex));
            return false;
        }
    }
}
