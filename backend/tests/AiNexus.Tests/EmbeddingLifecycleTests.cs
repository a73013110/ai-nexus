using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Persistence;
using AiNexus.Features.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using AiNexus.Features.Jobs;

namespace AiNexus.Tests;

public sealed class EmbeddingLifecycleTests
{
    private static async Task<IReadOnlyList<EmbeddingProfileDto>> Profiles(HttpClient client) => (await client.GetFromJsonAsync<EmbeddingProfileDto[]>("/api/v1/admin/knowledge/profiles"))!;
    private static async Task Drain(NexusFactory factory)
    {
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        while (await worker.ProcessNextAsync(CancellationToken.None)) { }
    }
    [Fact]
    public async Task BuildingRebuildActivateAndRetiredCleanupAreFencedAuditedAndProfileIsolated()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var client = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/admin/knowledge/profiles")).StatusCode);
        var seed = await RetrievalPipelineTests.SeedAsync(factory, client);
        var original = Assert.Single(await Profiles(client));
        factory.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value.Dimensions = 1024;
        var target = (await Profiles(client)).Single(x => x.Status == "building");
        Assert.False(target.Coverage.Complete); Assert.Equal(0, target.Coverage.CompletedChunks);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/v1/admin/knowledge/profiles/{target.Id}/activate", null)).StatusCode);
        (await client.PostAsync($"/api/v1/admin/knowledge/profiles/{target.Id}/rebuild", null)).EnsureSuccessStatusCode(); await Drain(factory);
        Assert.True((await Profiles(client)).Single(x => x.Id == target.Id).Coverage.Complete);
        (await client.PostAsync($"/api/v1/admin/knowledge/profiles/{target.Id}/activate", null)).EnsureSuccessStatusCode();
        var all = await Profiles(client); Assert.Equal(target.Id, all.Single(x => x.Status == "active").Id); Assert.Equal("retired", all.Single(x => x.Id == original.Id).Status);
        (await client.PostAsJsonAsync("/api/v1/knowledge/search", new KnowledgeSearchRequest("採購", [seed.Collection]))).EnsureSuccessStatusCode();
        Assert.Equal(target.Id, factory.Embeddings.LastProfileId);
        (await client.DeleteAsync($"/api/v1/admin/knowledge/profiles/{original.Id}/vectors")).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.Empty(await db.Set<ChunkEmbedding768>().ToListAsync()); Assert.NotEmpty(await db.Set<ChunkEmbedding1024>().ToListAsync()); Assert.NotEmpty(await db.Set<DocumentPage>().ToListAsync());
        Assert.Contains(await db.AuditEvents.ToListAsync(), x => x.Action == "admin.embedding_activate" && x.Result == "saved");
        Assert.Contains(await db.AuditEvents.ToListAsync(), x => x.Action == "admin.embedding_activate" && x.Result == "profile_incomplete");
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/v1/admin/knowledge/profiles/{target.Id}/vectors")).StatusCode);
    }
    [Fact]
    public async Task EditingIdenticalContentReusesVectorsAcrossDurableJobs()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync();
        var seed = await RetrievalPipelineTests.SeedAsync(factory, client); var calls = factory.Embeddings.Calls;
        var edited = await client.PutAsJsonAsync($"/api/v1/documents/{seed.Document.Id}/text", new TextDocumentRequest("採購規範", "採購應先核准。主管簽署後才可付款。", 1)); edited.EnsureSuccessStatusCode();
        await Drain(factory); Assert.Equal(calls, factory.Embeddings.Calls);
        Assert.Equal("ready", (await client.GetFromJsonAsync<DocumentDto>($"/api/v1/documents/{seed.Document.Id}"))!.Status);
    }
    [Fact]
    public async Task ActiveIndexCanBecomeReadyWhileBuildingIndexFailsAndRetryResumes()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var client = await factory.SignedInAsync();
        await Profiles(client); factory.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value.Revision = "next";
        var target = (await Profiles(client)).Single(x => x.Status == "building"); factory.Embeddings.FailProfileId = target.Id;
        var seed = await RetrievalPipelineTests.SeedAsync(factory, client);
        var document = (await client.GetFromJsonAsync<DocumentDto>($"/api/v1/documents/{seed.Document.Id}"))!;
        Assert.Equal("ready", document.Status); Assert.Equal("failed", (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{document.JobId}"))!.Status);
        factory.Embeddings.FailProfileId = null; (await client.PostAsync($"/api/v1/jobs/{document.JobId}/retry", null)).EnsureSuccessStatusCode(); await Drain(factory);
        Assert.True((await Profiles(client)).Single(x => x.Id == target.Id).Coverage.Complete);
    }
    [Fact]
    public async Task PartialReindexResumesCompletedBatchesWithoutRepeatingEmbedding()
    {
        await using var factory = new NexusFactory(administrators: ["alice"], services: services => services.PostConfigure<KnowledgeOptions>(x => {
            x.BatchSize = 1; x.ChunkTargetTokens = 80; x.ChunkMaxTokens = 100; x.ChunkMinTokens = 20; x.ChunkOverlapRatio = 0;
        }));
        using var client = await factory.SignedInAsync(); var seed = await RetrievalPipelineTests.SeedAsync(factory, client);
        var updated = await client.PutAsJsonAsync($"/api/v1/documents/{seed.Document.Id}/text", new TextDocumentRequest("採購規範", new string('甲', 90) + "。\n\n" + new string('乙', 90) + "。\n\n" + new string('丙', 90) + "。", 1)); updated.EnsureSuccessStatusCode(); await Drain(factory);
        var before = factory.Embeddings.Calls;
        factory.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value.Revision = "next"; var target = (await Profiles(client)).Single(x => x.Status == "building");
        factory.Embeddings.FailOnCall = before + 2;
        using var response = await client.PostAsync($"/api/v1/admin/knowledge/profiles/{target.Id}/rebuild", null); response.EnsureSuccessStatusCode(); var job = (await response.Content.ReadFromJsonAsync<JobDto>())!;
        await Drain(factory); Assert.Equal(1, (await Profiles(client)).Single(x => x.Id == target.Id).Coverage.CompletedChunks);
        factory.Embeddings.FailOnCall = null; (await client.PostAsync($"/api/v1/jobs/{job.Id}/retry", null)).EnsureSuccessStatusCode(); await Drain(factory);
        Assert.True((await Profiles(client)).Single(x => x.Id == target.Id).Coverage.Complete);
        Assert.Equal(before + 4, factory.Embeddings.Calls); // three distinct chunks plus one failed request
    }
    [Fact]
    public async Task NoneEmbeddingForcesKeywordAndModelProbeChecksRealBatchShape()
    {
        await using var factory = new NexusFactory(administrators: ["alice"], services: services => services.PostConfigure<KnowledgeOptions>(x => x.EmbeddingProvider = "none"));
        using var client = await factory.SignedInAsync(); var seed = await RetrievalPipelineTests.SeedAsync(factory, client);
        using var search = await client.PostAsJsonAsync("/api/v1/admin/knowledge/search", new AiNexus.Features.Administration.AdminRetrievalSearchRequest("採購", [seed.Collection], "vector")); search.EnsureSuccessStatusCode();
        Assert.Equal("keyword", (await search.Content.ReadFromJsonAsync<KnowledgeSearchDto>())!.Mode); Assert.Equal(0, factory.Embeddings.Calls);
        factory.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value.EmbeddingProvider = "ollama";
        using var probe = await client.PostAsync("/api/v1/admin/knowledge/capabilities/probe", null); probe.EnsureSuccessStatusCode();
        Assert.True((await probe.Content.ReadFromJsonAsync<RetrievalCapabilitiesDto>())!.Embedding.Available); Assert.Equal(1, factory.Embeddings.Calls);
    }
    [Fact]
    public async Task OpenApiIncludesRetrievalContracts()
    {
        await using var factory = new NexusFactory(); using var client = factory.CreateClient();
        using var result = await client.GetAsync("/openapi/v1.json"); result.EnsureSuccessStatusCode();
        var json = await result.Content.ReadAsStringAsync(); Assert.Contains("EmbeddingProfileDto", json); Assert.Contains("rewriteMs", json);
    }
}
