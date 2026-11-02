using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Knowledge;
using AiNexus.Features.Work.Knowledge.Embeddings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class EmbeddingProfileLockTests
{
    [Fact]
    public async Task ConcurrentFirstUseOfANewModelCreatesOneProfileAndKeepsOneActive()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        var original = await TargetAsync(factory);
        factory.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value.Embedding.Dimensions = 1024;

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => TargetAsync(factory)));

        Assert.Single(results.Select(x => x.Id).Distinct());
        Assert.All(results, x => Assert.Equal("building", x.Status));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(original.Id, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().EmbeddingProfiles.SingleAsync(x => x.Status == "active", ct)).Id);
    }

    // 每次索引都會問目前的模型；模型沒換時不能排在管理操作後面。
    [Fact]
    public async Task OnlyANewModelWaitsForTheRetrievalLock()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        await TargetAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        await using var transaction = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.BeginTransactionAsync(ct);
        Assert.True(await scope.ServiceProvider.GetRequiredService<AdministrativeWriteLock>().TryAcquireAsync(KnowledgeLocks.EmbeddingProfile, ct));

        await TargetAsync(factory).WaitAsync(TimeSpan.FromSeconds(30), ct);
        factory.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value.Embedding.Dimensions = 1024;
        var waiting = TargetAsync(factory);
        await Task.Delay(200, ct);
        Assert.False(waiting.IsCompleted);

        await transaction.CommitAsync(ct);
        Assert.Equal("building", (await waiting).Status);
    }

    private static async Task<AiNexus.Data.App.Knowledge.EmbeddingProfile> TargetAsync(AppFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EmbeddingProfiles>().TargetAsync(CancellationToken.None);
    }
}
