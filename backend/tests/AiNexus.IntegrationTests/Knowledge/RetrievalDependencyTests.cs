using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Retrieval;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class RetrievalDependencyTests
{
    [Fact]
    public async Task SqlServerRetrievalStoreResolvesWithProductionDependencies()
    {
        await using var factory = new NexusFactory();
        using var scope = factory.Services.CreateScope();
        // 即使此 host 使用 SQLite，仍要走正式環境的建構子。
        Assert.IsType<SqlServerRetrievalStore>(ActivatorUtilities.CreateInstance<SqlServerRetrievalStore>(scope.ServiceProvider));
    }

    [Fact]
    public async Task QueryRewriterAndBackgroundHandlersResolveWithScopeValidation()
    {
        await using var factory = new NexusFactory();
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
