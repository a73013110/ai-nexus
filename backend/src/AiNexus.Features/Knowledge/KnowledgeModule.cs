using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Indexing;
using AiNexus.Features.Knowledge.Embeddings;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Knowledge;

/// <summary>
/// Knowledge collections, documents and retrieval. Other modules use <see cref="DocumentService"/>,
/// <see cref="KnowledgeRetrieval"/> and <see cref="RetrievalAuthorization"/>; each endpoint use case has its own file.
/// </summary>
public sealed class KnowledgeModule : IFeatureModule
{
    public const string RetrievalModelsClient = "RetrievalModels";

    /// <summary>The configured embedding model, named in usage reports and price lists.</summary>
    public static ServiceModel EmbeddingModel(KnowledgeOptions options) => new(options.EmbeddingProvider, options.EmbeddingModel, "知識向量模型", "embedding-model");

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<KnowledgeOptions>().Configure<IConfiguration>((o, c) => KnowledgeSettings.Bind(c, o))
            .Validate(KnowledgeOptions.Valid, "知識檢索設定的維度、範圍或端點不正確。").ValidateOnStart();
        services.AddSingleton(sp => EmbeddingModel(sp.GetRequiredService<IOptions<KnowledgeOptions>>().Value));
        services.AddScoped<DocumentAccess>();
        services.AddScoped<IPrivateReaders, PrivateReaders>();
        services.AddScoped<ListLibraryFiles>();
        services.AddScoped<AddKnowledgeDocument>();
        services.AddScoped(provider => new DocumentService(provider.GetRequiredService<DocumentAccess>(), provider.GetRequiredService<AddKnowledgeDocument>()));
        services.AddScoped<CreateTextDocument>();
        services.AddScoped<UpdateTextDocument>();
        services.AddScoped<SaveKnowledgeCollection>();
        services.AddScoped<DeleteKnowledgeCollection>();
        services.AddScoped<SaveConversationKnowledge>();
        services.AddScoped<DeleteDocument>();
        services.AddScoped<ReindexDocument>();
        services.AddSingleton<KnowledgeWriteLock>();
        services.AddScoped<EmbeddingProfiles>();
        services.AddScoped<EmbeddingVectorStore>();
        services.AddScoped<DocumentIndexer>();
        services.AddScoped<EmbeddingLifecycle>();
        services.AddScoped<RetrievalModelProbe>();
        services.AddScoped<IBackgroundJobHandler, EmbeddingReindexHandler>();
        services.AddHostedService<EmbeddingBootstrapWorker>();
        // SQL Server uses native vector/full-text search; SQLite test fixtures use the in-memory ranking store.
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
        // Query rewriting uses ModelTaskService and its scoped database context.
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

    // Endpoint order is the published OpenAPI order.
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
        SearchDirectory.MapUsers(api);
        SearchDirectory.MapGroups(api);

        ListLibraryFiles.Map(api.MapGroup("/files").RequireAuthorization(Policies.Files).WithTags("File library"));
    }
}

internal sealed class KnowledgeFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Knowledge, "知識庫", "/knowledge", 30));
