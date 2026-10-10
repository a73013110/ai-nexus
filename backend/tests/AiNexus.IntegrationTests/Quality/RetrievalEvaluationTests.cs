using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Persistence;
using AiNexus.Features.Quality.RetrievalEvaluations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;

namespace AiNexus.IntegrationTests.Quality;

public sealed class RetrievalEvaluationTests
{
    private static RetrievalEvaluationRequest Request(Guid collection, Guid document) => new("固定採購驗收集", [collection], [new("採購核准", "採購如何核准？", [new(document, [1], 3)]), new("無答案", "火星登陸的燃料規範", [], true)]);

    private static async Task<RetrievalEvaluationDto> Create(HttpClient client, RetrievalEvaluationRequest request)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/quality/retrieval-evals", request); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<RetrievalEvaluationDto>())!;
    }

    [Fact]
    public async Task FourModesPersistOnlyMetricsAndExposeFallbackAndOwnerAcl()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync(); var seed = await KnowledgeApi.SeedAsync(factory, client);
        var run = await Create(client, Request(seed.Collection, seed.Document.Id)); await Drain(factory);
        var report = (await client.GetFromJsonAsync<RetrievalReportDto>($"/api/v1/quality/retrieval-evals/{run.Id}"))!;
        Assert.Equal("completed", report.Run.Job.Status); Assert.Equal(8, report.Results.Count); Assert.Equal(4, report.Summary.Count);
        Assert.Equal(1, report.Summary.Single(x => x.Mode == "keyword").RefusalRate);
        Assert.True(report.Summary.Single(x => x.Mode == "hybrid").Comparable);
        Assert.False(report.Summary.Single(x => x.Mode == "hybrid+rerank").Comparable);
        Assert.All(report.Results.Where(x => x.Mode == "hybrid+rerank"), x => Assert.Equal("hybrid(rerank-skipped)", x.ActualMode));
        var json = await client.GetStringAsync($"/api/v1/quality/retrieval-evals/{run.Id}/report");
        Assert.DoesNotContain("採購如何核准", json); Assert.DoesNotContain("主管簽署", json); Assert.DoesNotContain(seed.Document.Id.ToString(), json);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.DoesNotContain("採購應先核准", JsonSerializer.Serialize(await db.Set<RetrievalEvaluationResult>().ToListAsync()));
        using var bob = await factory.SignedInAsync("bob"); Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/quality/retrieval-evals/{run.Id}")).StatusCode);
        db.Remove(await db.Set<RoleGroupFeature>().SingleAsync(x => x.FeatureId == "knowledge")); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/quality/retrieval-evals/{run.Id}/report")).StatusCode);
    }

    [Fact]
    public async Task InterruptedEvaluationResumesCheckpointsAndRejectsChangedSettings()
    {
        await using var factory = new NexusFactory(services: services => services.PostConfigure<KnowledgeOptions>(x => x.Retrieval.QueryCacheMinutes = 0));
        using var client = await factory.SignedInAsync(); var seed = await KnowledgeApi.SeedAsync(factory, client);
        var run = await Create(client, Request(seed.Collection, seed.Document.Id)); factory.Embeddings.FailOnCall = factory.Embeddings.Calls + 2; await Drain(factory);
        var failed = (await client.GetFromJsonAsync<RetrievalReportDto>($"/api/v1/quality/retrieval-evals/{run.Id}"))!; Assert.Equal("failed", failed.Run.Job.Status); Assert.Equal(2, failed.Results.Count);
        var settings = factory.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value; settings.Retrieval.RrfK++;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/v1/jobs/{run.Job.Id}/retry", null)).StatusCode); settings.Retrieval.RrfK--;
        factory.Embeddings.FailOnCall = null; (await client.PostAsync($"/api/v1/jobs/{run.Job.Id}/retry", null)).EnsureSuccessStatusCode(); await Drain(factory);
        var complete = (await client.GetFromJsonAsync<RetrievalReportDto>($"/api/v1/quality/retrieval-evals/{run.Id}"))!;
        Assert.Equal("completed", complete.Run.Job.Status); Assert.Equal(8, complete.Results.Count);
        Assert.Equal(failed.Results.Single(x => x.Mode == "vector"), complete.Results.Single(x => x.Mode == "vector" && x.CaseId == "採購核准"));
    }

    [Fact]
    public async Task CorpusValidatesPagesScopeAndNoAnswerAnnotationsBeforeEnqueue()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync(); var seed = await KnowledgeApi.SeedAsync(factory, client);
        foreach (var relevance in new[] { new RetrievalRelevance(seed.Document.Id, [2]), new RetrievalRelevance(Guid.NewGuid(), [1]) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/quality/retrieval-evals", new RetrievalEvaluationRequest("錯誤驗收集", [seed.Collection], [new("一", "查詢", [relevance])]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/quality/retrieval-evals", new RetrievalEvaluationRequest("錯誤驗收集", [seed.Collection], [new("一", "查詢", [new(seed.Document.Id, [])], true)]))).StatusCode);
    }
}
