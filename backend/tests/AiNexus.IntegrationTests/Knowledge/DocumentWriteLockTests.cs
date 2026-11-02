using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge;
using AiNexus.Features.Work.Knowledge.Documents;
using AiNexus.Features.Work.Knowledge.Indexing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static AiNexus.IntegrationTests.Support.AttachmentApi;
using static AiNexus.IntegrationTests.Support.KnowledgeApi;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class DocumentWriteLockTests
{
    [Fact]
    public async Task ConcurrentReindexRequestsQueueOneJob()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        using var client = await factory.SignedInAsync();
        var seed = await SeedAsync(factory, client);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsync($"/api/v1/documents/{seed.Document.Id}/reindex", null, ct)));

        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
    }

    // 協作者是不同人，擁有者列鎖不會讓他們排隊：文件上限要靠知識庫這把鎖。
    [Fact]
    public async Task CollaboratorsCannotPassTheDocumentLimitTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        factory.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value.Indexing.MaxDocumentsPerCollection = 1;
        var collection = (await CreateCollection(alice)).Resource.Id;
        var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me", ct))!.Id;
        (await alice.PutAsJsonAsync($"/api/v1/knowledge/collections/{collection}/access", new ResourceAclRequest([new(bobId, "editor")], []), ct)).EnsureSuccessStatusCode();
        var files = await Task.WhenAll(Upload(alice, "a.txt", "甲"), Upload(bob, "b.txt", "乙"));

        var responses = await Task.WhenAll(
            alice.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection}/documents", new AddDocumentRequest(files[0].Id), ct),
            bob.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection}/documents", new AddDocumentRequest(files[1].Id), ct));

        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Documents.CountAsync(x => x.CollectionId == collection && !x.IsDeleted, ct));
    }

    [Fact]
    public async Task AWriteWaitsOnlyForItsOwnCollection()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        using var client = await factory.SignedInAsync();
        var busy = (await CreateCollection(client)).Resource.Id;
        var other = (await CreateCollection(client)).Resource.Id;
        var files = await Task.WhenAll(Upload(client, "a.txt", "甲"), Upload(client, "b.txt", "乙"));

        await using var scope = factory.Services.CreateAsyncScope();
        await using var transaction = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.BeginTransactionAsync(ct);
        Assert.True(await scope.ServiceProvider.GetRequiredService<DocumentWriteLock>().TryAcquireForCollectionAsync(busy, ct));

        (await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{other}/documents", new AddDocumentRequest(files[0].Id), ct).WaitAsync(TimeSpan.FromSeconds(30), ct)).EnsureSuccessStatusCode();
        var waiting = client.PostAsJsonAsync($"/api/v1/knowledge/collections/{busy}/documents", new AddDocumentRequest(files[1].Id), ct);
        await Task.Delay(200, ct);
        Assert.False(waiting.IsCompleted);

        await transaction.CommitAsync(ct);
        (await waiting).EnsureSuccessStatusCode();
    }

    // 模型重建與使用者重新索引可能同時處理同一份文件；片段清掉後兩邊都要寫同一組序號，不排隊就會撞上唯一索引。
    [Fact]
    public async Task ConcurrentPreparationsOfOneDocumentLeaveOneSetOfChunks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        using var client = await factory.SignedInAsync();
        var seed = await SeedAsync(factory, client);
        await using (var clear = factory.Services.CreateAsyncScope())
            await clear.ServiceProvider.GetRequiredService<AppDbContext>().Chunks.Where(x => x.DocumentId == seed.Document.Id).ExecuteDeleteAsync(ct);

        var results = await Task.WhenAll(PrepareAsync("test-first"), PrepareAsync("test-second"));

        Assert.All(results, x => Assert.True(x.IsSuccess));
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var ordinals = await db.Chunks.Where(x => x.DocumentId == seed.Document.Id).Select(x => x.Ordinal).ToListAsync(ct);
        Assert.NotEmpty(ordinals);
        Assert.Equal(ordinals.Count, (await db.Documents.SingleAsync(x => x.Id == seed.Document.Id, ct)).ChunkCount);

        async Task<AiNexus.Platform.Errors.Result> PrepareAsync(string kind)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var job = scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(seed.Actor, seed.Document.Id, seed.Document.Id, kind, "併行索引");
            var lease = Guid.NewGuid();
            (job.Status, job.LeaseToken, job.LeaseUntil) = (RunStates.Running, lease, DateTimeOffset.UtcNow.AddMinutes(5));
            await db.SaveChangesAsync(ct);
            var document = await db.Documents.SingleAsync(x => x.Id == seed.Document.Id, ct);
            return await scope.ServiceProvider.GetRequiredService<DocumentIndexer>().PrepareAsync(new JobExecution(db, job, lease), document, ct);
        }
    }
}
