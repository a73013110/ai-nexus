using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Operations;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiNexus.Tests;

public sealed class RetrievalEvaluationTests
{
    private static KnowledgeHitDto Hit(Guid document, int start = 1, int end = 1) => new(document, "來源原文不應存入報告", start, "來源原文不應存入報告", 1, Guid.NewGuid(), end);
    [Fact]
    public void MetricsUseGradedDocumentRelevanceAndPageRangesWithoutDoubleCredit()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var test = new RetrievalEvaluationCase("題一", "查詢", [new(a, [3], 3), new(b, [1], 1)]);
        var result = RetrievalMetrics.Measure(test, [Hit(c), Hit(a, 2, 4), Hit(a, 3, 3), Hit(b)], 4);
        Assert.Equal(1, result.Recall); Assert.Equal(.5, result.ReciprocalRank);
        Assert.Equal((7 / Math.Log2(3) + 1 / Math.Log2(5)) / (7 + 1 / Math.Log2(3)), result.Ndcg!.Value, 10);
        var wrongPage = RetrievalMetrics.Measure(test, [Hit(a), Hit(b)], 1);
        Assert.Equal(0, wrongPage.Recall); Assert.Equal(0, wrongPage.ReciprocalRank); Assert.Equal(0, wrongPage.Ndcg);
    }
    [Fact]
    public void NoAnswerAndLatencyPercentilesHaveExplicitDefinitions()
    {
        var test = new RetrievalEvaluationCase("無答案", "查詢", [], true);
        var refused = RetrievalMetrics.Measure(test, [], 6); Assert.True(refused.Refused); Assert.Null(refused.Recall);
        Assert.False(RetrievalMetrics.Measure(test, [Hit(Guid.NewGuid())], 6).Refused);
        var latency = RetrievalMetrics.Latency([10, 20, 30, 40]); Assert.Equal(25, latency.P50); Assert.Equal(38.5, latency.P95);
        Assert.Equal(new RetrievalLatencyDto(0, 0), RetrievalMetrics.Latency([]));
    }
    [Fact]
    public void IncompleteReportsAreNotMarkedComparable()
    {
        var result = new RetrievalMetricDto("一", "vector", "vector", null, 1, 1, 1, null, 0, 0, 1, 0, 1);
        Assert.False(RetrievalMetrics.Summarize([result], 2).Single(x => x.Mode == "vector").Comparable);
        Assert.True(RetrievalMetrics.Summarize([result], 1).Single(x => x.Mode == "vector").Comparable);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("[{}]")]
    [InlineData("[{\"index\":0,\"score\":\"bad\"}]")]
    [InlineData("[{\"index\":0,\"score\":1e999}]")]
    public void MalformedRerankerPayloadUsesDeclaredFailurePolicy(string json)
    {
        using var payload = JsonDocument.Parse(json);
        Assert.Equal("rerank_invalid", Assert.Throws<ApiException>(() => RerankPayload.Parse(payload.RootElement, "score")).Code);
    }
    private static async Task Drain(NexusFactory factory)
    {
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        while (await worker.ProcessNextAsync(CancellationToken.None)) { }
    }
    private static RetrievalEvaluationRequest Request(Guid collection, Guid document) => new("固定採購驗收集", [collection], [new("採購核准", "採購如何核准？", [new(document, [1], 3)]), new("無答案", "火星登陸的燃料規範", [], true)]);
    private static async Task<RetrievalEvaluationDto> Create(HttpClient client, RetrievalEvaluationRequest request)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/quality/retrieval-evals", request); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<RetrievalEvaluationDto>())!;
    }
    [Fact]
    public async Task FourModesPersistOnlyMetricsAndExposeFallbackAndOwnerAcl()
    {
        await using var factory = new NexusFactory(backgroundJobs: false); using var client = await factory.SignedInAsync(); var seed = await RetrievalPipelineTests.SeedAsync(factory, client);
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
        await using var factory = new NexusFactory(backgroundJobs: false, services: services => services.PostConfigure<KnowledgeOptions>(x => x.QueryCacheMinutes = 0));
        using var client = await factory.SignedInAsync(); var seed = await RetrievalPipelineTests.SeedAsync(factory, client);
        var run = await Create(client, Request(seed.Collection, seed.Document.Id)); factory.Embeddings.FailOnCall = factory.Embeddings.Calls + 2; await Drain(factory);
        var failed = (await client.GetFromJsonAsync<RetrievalReportDto>($"/api/v1/quality/retrieval-evals/{run.Id}"))!; Assert.Equal("failed", failed.Run.Job.Status); Assert.Equal(2, failed.Results.Count);
        var settings = factory.Services.GetRequiredService<IOptions<KnowledgeOptions>>().Value; settings.RrfK++;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/v1/jobs/{run.Job.Id}/retry", null)).StatusCode); settings.RrfK--;
        factory.Embeddings.FailOnCall = null; (await client.PostAsync($"/api/v1/jobs/{run.Job.Id}/retry", null)).EnsureSuccessStatusCode(); await Drain(factory);
        var complete = (await client.GetFromJsonAsync<RetrievalReportDto>($"/api/v1/quality/retrieval-evals/{run.Id}"))!;
        Assert.Equal("completed", complete.Run.Job.Status); Assert.Equal(8, complete.Results.Count);
        Assert.Equal(failed.Results.Single(x => x.Mode == "vector"), complete.Results.Single(x => x.Mode == "vector" && x.CaseId == "採購核准"));
    }
    [Fact]
    public async Task CorpusValidatesPagesScopeAndNoAnswerAnnotationsBeforeEnqueue()
    {
        await using var factory = new NexusFactory(backgroundJobs: false); using var client = await factory.SignedInAsync(); var seed = await RetrievalPipelineTests.SeedAsync(factory, client);
        foreach (var relevance in new[] { new RetrievalRelevance(seed.Document.Id, [2]), new RetrievalRelevance(Guid.NewGuid(), [1]) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/quality/retrieval-evals", new RetrievalEvaluationRequest("錯誤驗收集", [seed.Collection], [new("一", "查詢", [relevance])]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/quality/retrieval-evals", new RetrievalEvaluationRequest("錯誤驗收集", [seed.Collection], [new("一", "查詢", [new(seed.Document.Id, [])], true)]))).StatusCode);
    }
}
