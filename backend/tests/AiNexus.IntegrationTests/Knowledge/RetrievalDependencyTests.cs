using AiNexus.Features.Admin.Quality.RetrievalEvaluations;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Indexing;
using AiNexus.Features.Work.Knowledge.Retrieval;
using AiNexus.Platform.Modules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class RetrievalDependencyTests
{
    [Fact]
    public async Task SqlServerRetrievalStoreResolvesWithProductionDependencies()
    {
        await using var factory = new AppFactory();
        using var scope = factory.Services.CreateScope();
        Assert.IsType<SqlServerRetrievalStore>(scope.ServiceProvider.GetRequiredService<IRetrievalStore>());
    }

    [Fact]
    public async Task QueryRewriterAndBackgroundHandlersResolveWithScopeValidation()
    {
        await using var factory = new AppFactory();
        await using var app = factory.WithWebHostBuilder(builder => builder.UseDefaultServiceProvider(options => {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        }));
        using var first = app.Services.CreateScope();
        var rewriter = Assert.IsType<ModelQueryRewriter>(first.ServiceProvider.GetRequiredService<IQueryRewriter>());
        Assert.Same(rewriter, first.ServiceProvider.GetRequiredService<IQueryRewriter>());
        first.ServiceProvider.GetRequiredService<RetrievalPipeline>();
        var handlers = first.ServiceProvider.GetRequiredService<KeyedServices<IBackgroundJobHandler>>();
        Assert.IsType<DocumentIngestHandler>(handlers.Find(DocumentIngestHandler.JobKind));
        Assert.IsType<DocumentEmbeddingHandler>(handlers.Find(DocumentEmbeddingHandler.JobKind));
        Assert.IsType<RetrievalEvaluationHandler>(handlers.Find(RetrievalEvaluationHandler.JobKind));
        using var second = app.Services.CreateScope();
        Assert.NotSame(rewriter, second.ServiceProvider.GetRequiredService<IQueryRewriter>());
    }
}
