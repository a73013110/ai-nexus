using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Account;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;
using static AiNexus.IntegrationTests.Support.KnowledgeApi;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class SearchKnowledgeTests
{
    [Fact]
    public async Task CollectionAclPrefiltersSearchAndOriginalsAndRevocationStopsLaterRetrieval()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var collection = await CreateCollection(alice); var document = await AddDocument(alice, collection.Resource.Id, "Only approved staff may view this company policy."); await Process(factory);
        var request = new KnowledgeSearchRequest("company policy", [collection.Resource.Id]);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/v1/knowledge/search", request)).StatusCode);
        var calls = factory.Embeddings.Calls;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/documents/{document.Id}/content")).StatusCode);
        Assert.Equal(calls, factory.Embeddings.Calls);
        var user = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        (await alice.PutAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/access", new ResourceAclRequest([new(user.Id, "viewer")], []))).EnsureSuccessStatusCode();
        var result = await bob.PostAsJsonAsync("/api/v1/knowledge/search", request); result.EnsureSuccessStatusCode();
        Assert.Single((await result.Content.ReadFromJsonAsync<KnowledgeSearchDto>())!.Hits);
        Assert.Contains("approved staff", Encoding.UTF8.GetString(await bob.GetByteArrayAsync($"/api/v1/documents/{document.Id}/content")));
        var sharedJob = (await bob.GetFromJsonAsync<DocumentJobDto>($"/api/v1/documents/{document.Id}/job"))!;
        Assert.False(sharedJob.CanControl); Assert.Equal("completed", sharedJob.Job.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/jobs/{document.JobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.DeleteAsync($"/api/v1/documents/{document.Id}")).StatusCode);
        (await alice.PutAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/access", new ResourceAclRequest([], []))).EnsureSuccessStatusCode();
        calls = factory.Embeddings.Calls;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/v1/knowledge/search", request)).StatusCode);
        Assert.Equal(calls, factory.Embeddings.Calls);
    }

    // Only the effective-feature read of AccessService joins this table.
    private const string GrantRead = "RoleGroupFeatures";

    [Fact]
    public async Task SearchReadsCollectionAccessOnceBeforeRemoteWorkAndOnceBeforeReturning()
    {
        await using var factory = new NexusFactory(services: RequestQueries.Register);
        using var client = await factory.SignedInAsync(); var seed = await KnowledgeApi.SeedAsync(factory, client);
        var queries = factory.Services.GetRequiredService<RequestQueries>(); queries.Clear();
        using var response = await client.PostAsJsonAsync("/api/v1/knowledge/search", new KnowledgeSearchRequest("採購", [seed.Collection]));
        response.EnsureSuccessStatusCode();
        Assert.Single((await response.Content.ReadFromJsonAsync<KnowledgeSearchDto>())!.Hits);
        // Collection ACL: once at the start and once, with fresh grants, before the result leaves.
        Assert.Equal(2, queries.Count("/api/v1/knowledge/search", "\"ResourceGroups\""));
        Assert.Equal(2, queries.Count("/api/v1/knowledge/search", GrantRead));
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
        var result = (await scope.ServiceProvider.GetRequiredService<RetrievalPipeline>().SearchAsync(seed.Actor, new("採購", [seed.Collection]), CancellationToken.None, [new("user", "請查採購規範")])).Value!;
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
            Query = query; if (Fail) throw new ExternalServiceException(Error.Unavailable("fixture_rerank"), "測試重排失敗。");
            if (BeforeReturn is not null) await BeforeReturn();
            return candidates.Select((_, i) => new RerankScore(i, .5 - i * .01)).ToArray();
        }
    }

    internal sealed class FixtureRewriter : IQueryRewriter
    {
        public bool Fail { get; set; }
        public Task<string> RewriteAsync(Guid actor, string query, IReadOnlyList<RewriteTurn> history, CancellationToken ct) => Fail
            ? throw new ExternalServiceException(Error.Unavailable("fixture_rewrite"), "測試改寫失敗。") : Task.FromResult("採購核准");
    }
}
