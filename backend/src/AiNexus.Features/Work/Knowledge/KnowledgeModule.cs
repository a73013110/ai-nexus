using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Collections;
using AiNexus.Features.Work.Knowledge.Documents;
using AiNexus.Features.Work.Knowledge.Embeddings;
using AiNexus.Features.Work.Knowledge.Indexing;
using AiNexus.Features.Work.Knowledge.Retrieval;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
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
        services.AddSettings<KnowledgeOptions, KnowledgeOptionsValidator>(KnowledgeOptions.Section);
        services.AddSingleton(sp => EmbeddingModel(sp.GetRequiredService<IOptions<KnowledgeOptions>>().Value));
        services.AddScoped(provider => new DocumentService(provider.GetRequiredService<DocumentAccess>(), provider.GetRequiredService<AddKnowledgeDocument>()));
        services.AddBackgroundJobHandler<EmbeddingReindexHandler>(EmbeddingReindexHandler.JobKind);
        services.AddBackgroundJobHandler<EmbeddingClearHandler>(EmbeddingClearHandler.JobKind);
        // 鍵＝KnowledgeOptions 的 Provider 允許值；設定的供應商沒有實作時啟動失敗（VerifyStartup）。
        services.AddKeyedService<IRerankClient, NoneRerankClient>("none", ServiceLifetime.Singleton);
        services.AddKeyedService<IRerankClient, TeiRerankClient>("tei", ServiceLifetime.Singleton);
        services.AddKeyedService<IRerankClient, OpenAiCompatibleRerankClient>("openai-compatible", ServiceLifetime.Singleton);
        // 查詢改寫會用到 ModelTaskService 與其 scoped 的資料庫 context，因此這裡必須是 scoped，不可改成 singleton。
        services.AddScoped<IQueryRewriter>(s => s.GetRequiredService<IOptions<KnowledgeOptions>>().Value.QueryRewrite.Enabled
            ? ActivatorUtilities.CreateInstance<ModelQueryRewriter>(s) : new NoopQueryRewriter());
        services.AddKeyedService<IEmbeddingClient, OllamaEmbeddingClient>("ollama", ServiceLifetime.Singleton);
        services.AddKeyedService<IEmbeddingClient, GoogleEmbeddingClient>("google", ServiceLifetime.Singleton);
        services.AddKeyedService<IEmbeddingClient, NoneEmbeddingClient>("none", ServiceLifetime.Singleton);
        services.AddControlledHttpClient(RetrievalModelsClient);
        services.AddBackgroundJobHandler<DocumentIngestHandler>(DocumentIngestHandler.JobKind);
        services.AddBackgroundJobHandler<DocumentEmbeddingHandler>(DocumentEmbeddingHandler.JobKind);
    }

    /// <summary>設定的向量與重排供應商都要有實作，不等到第一次索引或檢索才失敗。</summary>
    public static void VerifyStartup(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<KnowledgeOptions>>().Value;
        _ = services.GetRequiredService<KeyedServices<IEmbeddingClient>>().Require(options.Embedding.Provider);
        _ = services.GetRequiredService<KeyedServices<IRerankClient>>().Require(options.Rerank.Provider);
    }

    // 端點註冊順序即為發布的 OpenAPI 順序；調整順序會改變 contracts/openapi.json 的內容。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/knowledge").RequireFeature(FeatureCatalog.Knowledge)
            .MapSlice<CreateTextDocument>()
            .MapSlice<ListKnowledgeCollections>()
            .MapSlice<SaveKnowledgeCollection>()
            .MapSlice<DeleteKnowledgeCollection>()
            .MapSlice<ListKnowledgeDocuments>()
            .MapSlice<AddKnowledgeDocument>()
            .MapSlice<ReadKnowledgeAccess>()
            .MapSlice<SaveKnowledgeAccess>()
            .MapSlice<SearchKnowledge>();

        api.MapGroup("/conversations/{id:guid}/knowledge").RequireFeature(FeatureCatalog.Knowledge).RequireFeature(FeatureCatalog.Chat)
            .MapSlice<GetConversationKnowledge>()
            .MapSlice<SaveConversationKnowledge>();

        api.MapGroup("/documents")
            .MapSlice<ReadTextDocument>()
            .MapSlice<UpdateTextDocument>()
            .MapSlice<GetDocument>()
            .MapSlice<ListDocumentPages>()
            .MapSlice<GetDocumentJob>()
            .MapSlice<DownloadDocumentOriginal>()
            .MapSlice<DeleteDocument>()
            .MapSlice<ReindexDocument>();

        api.MapGroup("/attachments").RequireAuthorization(Policies.Attachments)
            .MapSlice<ReadAttachmentDocument>();

        api.MapGroup("/files").RequireFeature(FeatureCatalog.Files)
            .MapSlice<ListLibraryFiles>();

        // 管理端的 embedding profile 與檢索測試，只給檢索管理頁使用。
        api.MapGroup("/admin/knowledge").RequireFeature(FeatureCatalog.KnowledgeRetrieval)
            .MapSlice<ManageEmbeddingProfiles>()
            .MapSlice<ReadRetrievalCapabilities>()
            .MapSlice<TestAdminRetrieval>();
    }
}
