using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments.Storage;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Embeddings;
using AiNexus.Features.Knowledge.Indexing;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge;

/// <summary>
/// 知識庫模組的組合根：註冊服務與端點。其他模組只透過 <see cref="DocumentService"/>、<see cref="KnowledgeRetrieval"/> 與 <see cref="RetrievalAuthorization"/> 存取。
/// </summary>
public sealed class KnowledgeModule : IFeatureModule
{
    public const string RetrievalModelsClient = "RetrievalModels";

    /// <summary>目前設定的向量模型，用於用量報表與價目表的模型名稱。</summary>
    public static ServiceModel EmbeddingModel(KnowledgeOptions options) => new(options.Embedding.Provider, options.Embedding.Model, "知識向量模型", "embedding-model");

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddDashboardSource<KnowledgeDashboard>();
        services.AddMonitoringSource<KnowledgeMonitoring>();
        services.AddSettings<KnowledgeOptions, KnowledgeOptionsValidator>(KnowledgeOptions.Section);
        services.AddSingleton(sp => EmbeddingModel(sp.GetRequiredService<IOptions<KnowledgeOptions>>().Value));
        services.AddScoped<DocumentAccess>();
        services.AddScoped<IPrivateReaders, PrivateReaders>();
        services.AddScoped(provider => new DocumentService(provider.GetRequiredService<DocumentAccess>(), provider.GetRequiredService<AddKnowledgeDocument>()));
        services.AddSingleton<KnowledgeWriteLock>();
        services.AddScoped<EmbeddingProfiles>();
        services.AddScoped<EmbeddingVectorStore>();
        services.AddScoped<DocumentIndexer>();
        services.AddScoped<EmbeddingLifecycle>();
        services.AddScoped<RetrievalModelProbe>();
        services.AddScoped<IBackgroundJobHandler, EmbeddingReindexHandler>();
        services.AddHostedService<EmbeddingBootstrapWorker>();
        // SQL Server 使用原生向量與全文檢索；SQLite 測試環境改用記憶體內的排序實作，結果排序細節可能與正式環境不同。
        services.AddScoped<IRetrievalStore>(s => s.GetRequiredService<NexusDbContext>().Database.IsSqlServer()
            ? ActivatorUtilities.CreateInstance<SqlServerRetrievalStore>(s) : ActivatorUtilities.CreateInstance<InMemoryRetrievalStore>(s));
        services.AddScoped<KnowledgeRetrieval>();
        services.AddScoped<RetrievalAuthorization>();
        services.AddScoped<RetrievalPipeline>();
        services.AddSingleton<QueryVectorCache>();
        services.AddSingleton<RerankService>();
        services.AddSingleton<IRerankClient, NoneRerankClient>();
        services.AddSingleton<IRerankClient, TeiRerankClient>();
        services.AddSingleton<IRerankClient, OpenAiCompatibleRerankClient>();
        // 查詢改寫會用到 ModelTaskService 與其 scoped 的資料庫 context，因此這裡必須是 scoped，不可改成 singleton。
        services.AddScoped<IQueryRewriter>(s => s.GetRequiredService<IOptions<KnowledgeOptions>>().Value.QueryRewrite.Enabled
            ? ActivatorUtilities.CreateInstance<ModelQueryRewriter>(s) : new NoopQueryRewriter());
        services.AddSingleton<RetrievalHttp>();
        services.AddSingleton<RetrievalInvocation>();
        services.AddSingleton<EmbeddingBatchScheduler>();
        services.AddSingleton<IEmbeddingClient, OllamaEmbeddingClient>();
        services.AddSingleton<IEmbeddingClient, GoogleEmbeddingClient>();
        services.AddSingleton<IEmbeddingClient, NoneEmbeddingClient>();
        services.AddSingleton<EmbeddingService>();
        services.AddSingleton<ITextChunker, StructuredChunker>();
        services.AddControlledHttpClient(RetrievalModelsClient);
        services.AddScoped<IBackgroundJobHandler, DocumentIngestHandler>();
        services.AddScoped<IBackgroundJobHandler, DocumentEmbeddingHandler>();
        services.AddFeaturePolicy(FeatureIds.Knowledge);
    }

    // 端點註冊順序即為發布的 OpenAPI 順序；調整順序會改變 contracts/openapi.json 的內容。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/knowledge").RequireAuthorization(Policies.Knowledge).WithTags("Knowledge");
        CreateTextDocument.Map(routes);
        ListKnowledgeCollections.Map(routes);
        SaveKnowledgeCollection.MapCreate(routes);
        SaveKnowledgeCollection.MapUpdate(routes);
        DeleteKnowledgeCollection.Map(routes);
        ListKnowledgeDocuments.Map(routes);
        AddKnowledgeDocument.MapCollection(routes);
        ReadKnowledgeAccess.Map(routes);
        SaveKnowledgeAccess.Map(routes);
        SearchKnowledge.Map(routes);

        var selection = api.MapGroup("/conversations/{id:guid}/knowledge").RequireAuthorization(Policies.Knowledge).RequireAuthorization(Policies.Chat).WithTags("Knowledge");
        GetConversationKnowledge.Map(selection);
        SaveConversationKnowledge.Map(selection);

        var documents = api.MapGroup("/documents").WithTags("Documents");
        ReadTextDocument.Map(documents);
        UpdateTextDocument.Map(documents);
        GetDocument.Map(documents);
        ListDocumentPages.Map(documents);
        GetDocumentJob.Map(documents);
        DownloadDocumentOriginal.Map(documents);
        DeleteDocument.Map(documents);
        ReindexDocument.Map(documents);

        AddKnowledgeDocument.MapAttachment(api);

        ListLibraryFiles.Map(api.MapGroup("/files").RequireAuthorization(Policies.Files).WithTags("File library"));
    }
}

internal sealed class KnowledgeFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Knowledge, "知識庫", "/knowledge", 30, BuiltInAccess.WorkNavigation, "library"));
