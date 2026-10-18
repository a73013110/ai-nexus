using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Retrieval;
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
        var handlers = first.ServiceProvider.GetServices<IBackgroundJobHandler>().ToArray();
        Assert.Contains(handlers, x => x.Kind == "document-ingest");
        Assert.Contains(handlers, x => x.Kind == "document-embedding");
        Assert.Contains(handlers, x => x.Kind == "retrieval-eval");
        using var second = app.Services.CreateScope();
        Assert.NotSame(rewriter, second.ServiceProvider.GetRequiredService<IQueryRewriter>());
    }
}
