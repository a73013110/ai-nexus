using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Collections;
using AiNexus.Features.Work.Knowledge.Documents;
using AiNexus.Features.Work.Knowledge.Embeddings;
using AiNexus.Features.Work.Knowledge.Indexing;
using AiNexus.Features.Work.Knowledge.Retrieval;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Knowledge;

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
        services.AddScoped<IRetrievalStore, SqlServerRetrievalStore>();
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
        services.AddFeaturePolicy(FeatureCatalog.Knowledge);
        services.AddFeaturePolicy(FeatureCatalog.KnowledgeRetrieval);
    }

    // 端點註冊順序即為發布的 OpenAPI 順序；調整順序會改變 contracts/openapi.json 的內容。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/knowledge").RequireAuthorization(Policies.Knowledge);
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

        var selection = api.MapGroup("/conversations/{id:guid}/knowledge").RequireAuthorization(Policies.Knowledge).RequireAuthorization(Policies.Chat);
        GetConversationKnowledge.Map(selection);
        SaveConversationKnowledge.Map(selection);

        var documents = api.MapGroup("/documents");
        ReadTextDocument.Map(documents);
        UpdateTextDocument.Map(documents);
        GetDocument.Map(documents);
        ListDocumentPages.Map(documents);
        GetDocumentJob.Map(documents);
        DownloadDocumentOriginal.Map(documents);
        DeleteDocument.Map(documents);
        ReindexDocument.Map(documents);

        AddKnowledgeDocument.MapAttachment(api);

        ListLibraryFiles.Map(api.MapGroup("/files").RequireAuthorization(Policies.Files));

        // 管理端的 embedding profile 與檢索測試，只給檢索管理頁使用。
        var admin = api.MapGroup("/admin/knowledge").RequireAuthorization(Policies.KnowledgeRetrieval);
        ManageEmbeddingProfiles.MapList(admin);
        ManageEmbeddingProfiles.MapRebuild(admin);
        ManageEmbeddingProfiles.MapActivate(admin);
        ManageEmbeddingProfiles.MapClear(admin);
        ReadRetrievalCapabilities.MapGet(admin);
        ReadRetrievalCapabilities.MapProbe(admin);
        TestAdminRetrieval.Map(admin);
    }
}
