using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using AiNexus.Features.Jobs;

namespace AiNexus.Tests;

public sealed class RetrievalPipelineTests
{
    private static KnowledgeHitDto Hit(Guid document, int ordinal, string text = "原文。", int page = 1) => new(document, "規範", page, text, 0, Guid.NewGuid(), page, ordinal);
    [Fact]
    public void RrfCombinesBothRanksAndHonorsWeights()
    {
        var doc = Guid.NewGuid(); var a = Hit(doc, 0); var b = Hit(doc, 1); var c = Hit(doc, 2);
        var result = RetrievalRanking.Fuse([a, b], [c, b], new() { FtsWeight = 2 });
        Assert.Equal(b.ChunkId, result[0].ChunkId); Assert.Equal(2, result[0].VectorRank); Assert.Equal(2, result[0].FtsRank);
        Assert.Equal(3.0 / 62, result[0].RrfScore!.Value, 10); Assert.Equal(3, result.Count);
    }
    [Fact]
    public void DiversityAndAdjacentMergeRemoveSentenceOverlapAndRetainPageRange()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var result = RetrievalRanking.Context([Hit(a, 1, "重複句。第二頁。", 2), Hit(a, 0, "第一頁。重複句。"), Hit(a, 2), Hit(b, 0)], new() { MaxChunksPerDocument = 2 });
        Assert.Equal(2, result.Count); Assert.Equal("第一頁。重複句。第二頁。", result[0].Text); Assert.Equal(1, result[0].PageNumber); Assert.Equal(2, result[0].EndPage);
        Assert.Equal(b, result[1].DocumentId);
    }
    [Fact]
    public void ContextBudgetIncludesHeadersAndNeverCutsSurrogates()
    {
        var result = RetrievalRanking.Context([Hit(Guid.NewGuid(), 0, new string('文', 300) + "😀")], new() { ContextTokens = 100 });
        Assert.True(TokenEstimator.Estimate(result[0].Text + result[0].Title) + 32 <= 100);
        Assert.False(char.IsHighSurrogate(result[0].Text[^1]));
    }
    internal static async Task<(Guid Collection, Guid Actor, DocumentDto Document)> SeedAsync(NexusFactory factory, HttpClient client)
    {
        using var created = await client.PostAsJsonAsync("/api/v1/knowledge/collections", new CollectionRequest("採購規範", "測試")); created.EnsureSuccessStatusCode();
        var collection = (await created.Content.ReadFromJsonAsync<CollectionDto>())!.Resource.Id;
        using var added = await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection}/text", new TextDocumentRequest("採購規範", "採購應先核准。主管簽署後才可付款。")); added.EnsureSuccessStatusCode();
        var document = (await added.Content.ReadFromJsonAsync<DocumentDto>())!;
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services); Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        while (await worker.ProcessNextAsync(CancellationToken.None)) { }
        var actor = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        return (collection, actor, document);
    }
    [Fact]
    public async Task HybridUsesBothRanksAndQueryCacheAvoidsDuplicateInvocation()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync(); var seed = await SeedAsync(factory, client);
        var before = factory.Embeddings.Calls;
        foreach (var query in new[] { "  採購   核准  ", "採購 核准" })
        {
            using var response = await client.PostAsJsonAsync("/api/v1/knowledge/search", new KnowledgeSearchRequest(query, [seed.Collection])); response.EnsureSuccessStatusCode();
            var result = (await response.Content.ReadFromJsonAsync<KnowledgeSearchDto>())!;
            Assert.Equal("hybrid", result.Mode); Assert.NotNull(Assert.Single(result.Hits).VectorRank); Assert.NotNull(result.Hits[0].FtsRank);
        }
        Assert.Equal(before + 1, factory.Embeddings.Calls);
    }
    [Theory]
    [InlineData("skip", HttpStatusCode.OK, "hybrid(rerank-skipped)")]
    [InlineData("fail", HttpStatusCode.ServiceUnavailable, null)]
    public async Task RerankFailurePolicyIsExplicit(string policy, HttpStatusCode status, string? mode)
    {
        await using var factory = new NexusFactory(services: services => {
            services.PostConfigure<KnowledgeOptions>(x => x.Rerank = new() { Provider = "tei", Endpoint = "http://fixture", FailurePolicy = policy });
            services.RemoveAll<IRerankClient>(); services.AddSingleton<IRerankClient>(new FixtureReranker { Fail = true });
        });
        using var client = await factory.SignedInAsync(); var seed = await SeedAsync(factory, client);
        using var response = await client.PostAsJsonAsync("/api/v1/knowledge/search", new KnowledgeSearchRequest("採購", [seed.Collection]));
        Assert.Equal(status, response.StatusCode);
        if (mode is not null) Assert.Equal(mode, (await response.Content.ReadFromJsonAsync<KnowledgeSearchDto>())!.Mode);
    }
    [Fact]
    public async Task FailedRewriteUsesOriginalAndSuccessfulRerankAppliesRefusalThreshold()
    {
        var rerank = new FixtureReranker();
        await using var factory = new NexusFactory(services: services => {
            services.PostConfigure<KnowledgeOptions>(x => x.Rerank = new() { Provider = "tei", Endpoint = "http://fixture", MinScore = .9 });
            services.RemoveAll<IRerankClient>(); services.AddSingleton<IRerankClient>(rerank);
            services.RemoveAll<IQueryRewriter>(); services.AddSingleton<IQueryRewriter>(new FixtureRewriter { Fail = true });
        });
        using var client = await factory.SignedInAsync(); var seed = await SeedAsync(factory, client);
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<RetrievalPipeline>().SearchAsync(seed.Actor, new("採購", [seed.Collection]), CancellationToken.None, [new("user", "請查採購規範")]);
        Assert.Equal("hybrid+rerank(rewrite-skipped)", result.Mode); Assert.Empty(result.Hits); Assert.Equal("採購", rerank.Query);
    }
    [Fact]
    public async Task AclRevokedDuringRerankCannotReturnSourceText()
    {
        var rerank = new FixtureReranker();
        await using var factory = new NexusFactory(services: services => {
            services.PostConfigure<KnowledgeOptions>(x => x.Rerank = new() { Provider = "tei", Endpoint = "http://fixture" });
            services.RemoveAll<IRerankClient>(); services.AddSingleton<IRerankClient>(rerank);
        });
        using var client = await factory.SignedInAsync(); var seed = await SeedAsync(factory, client);
        rerank.BeforeReturn = async () => { using var other = factory.Services.CreateScope(); var db = other.ServiceProvider.GetRequiredService<NexusDbContext>();
            await db.Set<AiNexus.Features.AccessControl.UserRole>().Where(x => x.UserId == seed.Actor).ExecuteDeleteAsync(); };
        using var response = await client.PostAsJsonAsync("/api/v1/knowledge/search", new KnowledgeSearchRequest("採購", [seed.Collection]));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.DoesNotContain("主管簽署", await response.Content.ReadAsStringAsync());
    }
    internal sealed class FixtureReranker : IRerankClient
    {
        public string Provider => "tei"; public bool Fail { get; set; } public string? Query { get; private set; } public Func<Task>? BeforeReturn { get; set; }
        public async Task<IReadOnlyList<RerankScore>> RerankAsync(string query, IReadOnlyList<string> candidates, CancellationToken ct)
        {
            Query = query; if (Fail) throw new ApiException(503, "fixture_rerank", "測試重排失敗。");
            if (BeforeReturn is not null) await BeforeReturn();
            return candidates.Select((_, i) => new RerankScore(i, .5 - i * .01)).ToArray();
        }
    }
    internal sealed class FixtureRewriter : IQueryRewriter
    {
        public bool Fail { get; set; }
        public Task<string> RewriteAsync(Guid actor, string query, IReadOnlyList<RewriteTurn> history, CancellationToken ct) => Fail
            ? throw new ApiException(503, "fixture_rewrite", "測試改寫失敗。") : Task.FromResult("採購核准");
    }
}
