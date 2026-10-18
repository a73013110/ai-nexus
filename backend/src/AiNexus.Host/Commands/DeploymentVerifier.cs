using AiNexus.Data.App;
using AiNexus.Features.Work.Artifacts.Export;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Admin.Integrations;
using AiNexus.Features.Work.Knowledge;
using AiNexus.Features.Work.Knowledge.Retrieval;
using AiNexus.Features.Work.Repositories;
using AiNexus.Features.Platform.WebSearch;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AiNexus.Host.Commands;

/// <summary>
/// 系統部署就緒度驗證器。
/// 執行唯讀 SQL 檢查、設定檢驗、模型清單盤點、合成 Embedding/Rerank 探針以及暫時性儲存目錄寫入測試。
/// 不會呼叫大型語言模型產生回答，因此不消耗正式環境的提問配額。
/// </summary>
public static class DeploymentVerifier
{
    private static readonly System.Text.Json.JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// 驗證部署環境之資料庫、認證、模型、附件儲存與診斷日誌是否健全。
    /// </summary>
    public static async Task<bool> VerifyAsync(IServiceProvider services, IConfiguration config, IHostEnvironment environment, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        try
        {
            var inference = scope.ServiceProvider.GetRequiredService<IOptions<InferenceOptions>>().Value;
            var ad = scope.ServiceProvider.GetRequiredService<IOptions<AdAuthenticationOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<AdminBootstrapOptions>>().Value;
            var knowledge = scope.ServiceProvider.GetRequiredService<IOptions<KnowledgeOptions>>().Value;
            var attachments = scope.ServiceProvider.GetRequiredService<IOptions<FilesOptions>>().Value;
            var diagnostics = scope.ServiceProvider.GetRequiredService<IOptions<AiNexus.Platform.Diagnostics.DiagnosticOptions>>().Value;

            var diagnosticPath = AiNexus.Platform.Diagnostics.DiagnosticJournal.Resolve(diagnostics.Directory, environment);
            Directory.CreateDirectory(diagnosticPath);
            var diagnosticProbe = Path.Combine(diagnosticPath, ".verify-" + Guid.NewGuid().ToString("N"));
            try
            {
                await File.WriteAllTextAsync(diagnosticProbe, "AiNexus diagnostic storage write probe", ct);
            }
            finally
            {
                if (File.Exists(diagnosticProbe))
                {
                    File.Delete(diagnosticProbe);
                }
            }

            _ = scope.ServiceProvider.GetRequiredService<IOptions<ExportOptions>>().Value;
            _ = scope.ServiceProvider.GetRequiredService<IOptions<IntegrationsOptions>>().Value;
            var search = scope.ServiceProvider.GetRequiredService<IOptions<WebSearchOptions>>().Value;
            var gitea = scope.ServiceProvider.GetRequiredService<IOptions<GiteaOptions>>().Value;
            var sql = new SqlConnectionStringBuilder(scope.ServiceProvider.GetRequiredService<IOptions<DatabasesOptions>>().Value.ConnectionString(AppDbContext.DatabaseName));
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var connected = await db.Database.CanConnectAsync(ct);
            var providerConfigured = !inference.ProviderConcurrency.ContainsKey("google") || !string.IsNullOrWhiteSpace(inference.Providers.Google.ApiKey);
            var catalog = await scope.ServiceProvider.GetRequiredService<ModelCatalog>().GetAsync(ct);
            await scope.ServiceProvider.GetRequiredService<IAttachmentStorage>().VerifyAsync(ct);

            var retrieval = await scope.ServiceProvider.GetRequiredService<RetrievalModelProbe>().CheckAsync(null, ct);
            var ready = connected
                && catalog.ProviderAvailable
                && catalog.Models.Count > 0
                && providerConfigured
                && (ad.Mode == "Windows" || ad.Configured)
                && retrieval.Embedding.Available == true
                && retrieval.Rerank.Available == true;

            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                environment = environment.EnvironmentName,
                sqlConnected = connected,
                sqlEncrypted = sql.Encrypt != SqlConnectionEncryptOption.Optional,
                trustsSqlCertificate = sql.TrustServerCertificate,
                authMode = ad.Mode,
                adConfigured = ad.Mode == "Windows" || ad.Configured,
                providers = catalog.Providers,
                providerConfigured,
                configuredModelCount = inference.Models.Count,
                providerAvailable = catalog.ProviderAvailable,
                availableModelCount = catalog.Models.Count,
                modelNotice = catalog.Notice,
                embeddingProvider = knowledge.Embedding.Provider,
                embeddingDimensions = knowledge.Embedding.Dimensions,
                embeddingAvailable = retrieval.Embedding.Available,
                embeddingNotice = retrieval.Embedding.Notice,
                rerankAvailable = retrieval.Rerank.Available,
                rerankNotice = retrieval.Rerank.Notice,
                webSearchEnabled = search.Enabled,
                giteaEnabled = gitea.Enabled,
                keyRingPath = config["DataProtection:KeyRingPath"],
                attachmentStoragePath = attachments.StoragePath,
                attachmentStorageWritable = true,
                diagnosticStoragePath = diagnosticPath,
                diagnosticStorageWritable = true,
                diagnosticCapacityBytes = diagnostics.MaxDiskBytes,
                diagnosticMaxSqlRows = diagnostics.MaxSqlRows,
                diagnosticOtlpEnabled = diagnostics.OtlpEnabled,
                ready
            }, Indented));

            return ready;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex is ExternalServiceException external
                ? external.Message
                : ex is Microsoft.Extensions.Options.OptionsValidationException
                    ? "部署組態驗證失敗。請檢查資料庫、模型、身分驗證、外部工具、連線器與配額設定。"
                    : DatabaseDiagnosis.Describe(ex, AppDbContext.DatabaseName));
            return false;
        }
    }
}
