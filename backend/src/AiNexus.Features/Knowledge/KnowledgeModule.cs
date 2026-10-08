using AiNexus.Features.AccessControl;
using AiNexus.Features.Configuration;
using AiNexus.Features.Operations;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge;

public sealed class KnowledgeModule : IFeatureModule
{
    public const string RetrievalModelsClient = "RetrievalModels";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<KnowledgeOptions>().Configure<IConfiguration>((o, c) => NexusSettings.Knowledge(c, o))
            .Validate(KnowledgeOptions.Valid, "知識檢索設定的維度、範圍或端點不正確。").ValidateOnStart();
        services.AddScoped<DocumentService>();
        services.AddScoped<TextDocumentService>();
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
        services.AddFeaturePolicy("knowledge");
    }

    public static void MapEndpoints(RouteGroupBuilder api) => KnowledgeEndpoints.MapKnowledge(api);
}
