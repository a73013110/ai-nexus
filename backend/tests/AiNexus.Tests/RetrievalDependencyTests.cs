using AiNexus.Features.Knowledge;
using AiNexus.Features.Operations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiNexus.Tests;

public sealed class RetrievalDependencyTests
{
    [Fact]
    public async Task SqlServerRetrievalStoreResolvesWithProductionDependencies()
    {
        await using var factory = new NexusFactory(backgroundJobs: false);
        using var scope = factory.Services.CreateScope();
        // Exercise the production constructor even though this host uses SQLite.
        Assert.IsType<SqlServerRetrievalStore>(ActivatorUtilities.CreateInstance<SqlServerRetrievalStore>(scope.ServiceProvider));
    }

    [Fact]
    public async Task QueryRewriterAndBackgroundHandlersResolveWithScopeValidation()
    {
        await using var factory = new NexusFactory(backgroundJobs: false);
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
