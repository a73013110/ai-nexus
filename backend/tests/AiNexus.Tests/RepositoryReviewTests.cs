using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using AiNexus.Modules.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AiNexus.Tests;

public sealed class RepositoryReviewTests
{
    private static NexusFactory Factory(FixtureGitea source, ReviewProvider provider) => new(backgroundJobs: false, services: services =>
    {
        services.RemoveAll<IGiteaClient>(); services.AddSingleton<IGiteaClient>(source);
        services.PostConfigure<GiteaOptions>(o => o.Enabled = true);
        services.RemoveAllKeyed<IInferenceProvider>("google"); services.AddKeyedSingleton<IInferenceProvider>("google", provider);
    });

    private static async Task<RepositoryReviewDto> Create(HttpClient client, string purpose = "review")
    {
        (await client.PostAsJsonAsync("/api/v1/repositories/connection", new ConnectRepositoryRequest("fixtureOnlyReadTokenForGitea00001"))).EnsureSuccessStatusCode();
        var response = await client.PostAsJsonAsync("/api/v1/repositories/reviews", new CreateRepositoryReviewRequest("hanglong/nexus", new string('a', 40), null, "test-model", "", Guid.NewGuid().ToString(), purpose));
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<RepositoryReviewDto>())!;
    }

    private static async Task Process(NexusFactory factory)
    {
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
    }

    private static string Diff(int files, int size) => string.Concat(Enumerable.Range(0, files).Select(i =>
        $"diff --git a/file-{i}.cs b/file-{i}.cs\n@@ -1 +1 @@\n-old\n+{new string('x', size)}\n"));

    [Theory]
    [InlineData("review", "整體程式碼檢閱")]
    [InlineData("summary", "理解整體變更")]
    [InlineData("typos", "找內容誤植")]
    public async Task SmallCrossFileChangesProduceOnePurposeSpecificReport(string purpose, string focus)
    {
        var source = new FixtureGitea { Diff = Diff(2, 10) }; var provider = new ReviewProvider();
        await using var factory = Factory(source, provider); using var client = await factory.SignedInAsync();
        var review = await Create(client, purpose); await Process(factory);
        var detail = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.Equal("completed", detail.Review.Job.Status); Assert.Equal(purpose, detail.Review.Purpose); Assert.Equal(2, detail.Version);
        var call = Assert.Single(provider.Calls); Assert.Contains(focus, call.Parameters.SystemPrompt);
        Assert.Contains("file-0.cs", call.Prompt); Assert.Contains("file-1.cs", call.Prompt);
        Assert.NotNull(detail.Report); Assert.Contains("file-0.cs", detail.Report.Output); Assert.Contains("file-1.cs", detail.Report.Output);
        Assert.All(detail.Sections, x => Assert.Null(x.Output)); Assert.False(detail.Report.Truncated);
    }

    [Fact]
    public async Task LargeReviewsReduceAllEvidenceAndRetryOnlyTheUnfinishedOverallReport()
    {
        var source = new FixtureGitea { Diff = Diff(3, 6000) }; var provider = new ReviewProvider { VerboseAnalysis = true, FailReportOnce = true };
        await using var factory = Factory(source, provider); using var client = await factory.SignedInAsync();
        var review = await Create(client); await Process(factory);
        var failed = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.Equal("failed", failed.Review.Job.Status); Assert.Null(failed.Report); Assert.All(failed.Sections, x => Assert.NotNull(x.Output));
        Assert.Contains(provider.Calls, x => x.Parameters.SystemPrompt.Contains("中間彙整"));
        var completedCalls = provider.Calls.Count;
        (await client.PostAsync($"/api/v1/repositories/reviews/{review.Id}/retry", null)).EnsureSuccessStatusCode(); await Process(factory);
        var detail = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.Equal("completed", detail.Review.Job.Status); Assert.NotNull(detail.Report); Assert.Equal(completedCalls + 1, provider.Calls.Count);
        Assert.Equal(failed.Sections.Select(x => x.Output), detail.Sections.Select(x => x.Output));
        for (var i = 0; i < 3; i++) Assert.Contains($"file-{i}.cs", detail.Report.Output);
        Assert.All(provider.Calls, call => Assert.True(Encoding.UTF8.GetByteCount(call.Prompt + call.Parameters.SystemPrompt) + call.Parameters.MaxOutputTokens + 160 <= call.Parameters.ContextTokens));
    }

    [Fact]
    public async Task OversizedIntermediateOutputFailsExplicitlyWithoutDroppingEvidenceAndCanResume()
    {
        var source = new FixtureGitea { Diff = Diff(2, 6000) }; var provider = new ReviewProvider { VerboseAnalysis = true, VerboseReduction = true };
        await using var factory = Factory(source, provider); using var client = await factory.SignedInAsync();
        var review = await Create(client); await Process(factory);
        var failed = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.Equal("review_summary_too_long", failed.Review.Job.ErrorCode); Assert.Null(failed.Report);
        Assert.All(failed.Sections, x => Assert.NotNull(x.Output));
        var analysisCalls = provider.Calls.Count(x => x.Parameters.SystemPrompt.Contains("一個區段"));
        provider.VerboseReduction = false;
        (await client.PostAsync($"/api/v1/repositories/reviews/{review.Id}/retry", null)).EnsureSuccessStatusCode(); await Process(factory);
        var detail = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.NotNull(detail.Report); Assert.Equal("completed", detail.Review.Job.Status);
        Assert.Equal(analysisCalls, provider.Calls.Count(x => x.Parameters.SystemPrompt.Contains("一個區段")));
    }

    [Fact]
    public async Task TruncatedEvidenceIsFlaggedInTheOverallReport()
    {
        var source = new FixtureGitea { Diff = Diff(2, 6000) }; var provider = new ReviewProvider { TruncateAnalysis = true };
        await using var factory = Factory(source, provider); using var client = await factory.SignedInAsync();
        var review = await Create(client); await Process(factory);
        var detail = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.NotNull(detail.Report); Assert.True(detail.Report.Truncated);
        Assert.Contains("檢閱不完整", provider.Calls.Last().Prompt);
    }

    [Fact]
    public async Task BinaryOnlyChangesProduceAManualCheckReportWithoutInvokingTheModel()
    {
        var source = new FixtureGitea { Diff = "diff --git a/image.png b/image.png\nGIT binary patch\n" + new string('x', 6000) }; var provider = new ReviewProvider();
        await using var factory = Factory(source, provider); using var client = await factory.SignedInAsync();
        var review = await Create(client); await Process(factory);
        var detail = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.Empty(provider.Calls); Assert.NotNull(detail.Report); Assert.Contains("人工確認", detail.Report.Output); Assert.All(detail.Sections, x => Assert.True(x.Binary));
    }

    [Fact]
    public async Task LegacySnapshotsRemainReadableAndRetryWithTheirFrozenSectionInstruction()
    {
        var source = new FixtureGitea { Diff = Diff(2, 10) }; var provider = new ReviewProvider();
        await using var factory = Factory(source, provider); using var client = await factory.SignedInAsync();
        var review = await Create(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var row = await db.Set<RepositoryReview>().SingleAsync(x => x.Id == review.Id);
            row.SnapshotJson = JsonSerializer.Serialize(new ReviewSnapshot(1, "舊版固定檢閱指令", RepositoryReviewService.Split(source.Diff, 1000)));
            db.Add(new RepositoryReviewResult { ReviewId = review.Id, Ordinal = 0, Output = "已完成的舊版區段" }); await db.SaveChangesAsync();
        }
        await Process(factory);
        var detail = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.Equal(1, detail.Version); Assert.Null(detail.Report); Assert.Equal("completed", detail.Review.Job.Status);
        Assert.Equal("已完成的舊版區段", detail.Sections[0].Output); Assert.Equal("舊版固定檢閱指令", Assert.Single(provider.Calls).Parameters.SystemPrompt);
    }

    [Fact]
    public void UnicodeSourceAndEvidenceArePreservedWithinTheByteBudget()
    {
        var text = string.Concat(Enumerable.Repeat("+中文🙂保留整行內容\n", 300));
        var diff = "diff --git a/中文.cs b/中文.cs\n" + text;
        var slices = RepositoryReviewService.Split(diff, 512);
        Assert.Equal(diff, string.Concat(slices.Select(x => x.Diff)));
        var strict = new UTF8Encoding(false, true);
        Assert.All(slices, x => Assert.InRange(strict.GetByteCount(x.Diff), 1, 512));
        var batches = RepositoryReviewPlan.Batches([text], 512);
        Assert.Equal(text, string.Concat(batches)); Assert.All(batches, x => Assert.InRange(strict.GetByteCount(x), 1, 512));
        Assert.Throws<ApiException>(() => RepositoryReviewService.Split(diff, 8));
    }

    [Fact]
    public async Task UnsupportedPurposeIsRejectedBeforeEnqueuingWork()
    {
        var provider = new ReviewProvider(); await using var factory = Factory(new(), provider); using var client = await factory.SignedInAsync();
        var response = await client.PostAsJsonAsync("/api/v1/repositories/reviews", new CreateRepositoryReviewRequest("hanglong/nexus", new string('a', 40), null, "test-model", "", Guid.NewGuid().ToString(), "unsupported"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Empty(provider.Calls);
        using var scope = factory.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<RepositoryReview>().ToListAsync());
    }

    private sealed class ReviewProvider : IInferenceProvider
    {
        public List<(string Prompt, GenerationParameters Parameters)> Calls { get; } = [];
        public bool VerboseAnalysis { get; init; }
        public bool VerboseReduction { get; set; }
        public bool FailReportOnce { get; set; }
        public bool TruncateAnalysis { get; init; }
        public Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "test-model" });
        public async IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, [EnumeratorCancellation] CancellationToken ct)
        {
            var prompt = messages.Single().Content; Calls.Add((prompt, parameters)); await Task.Yield(); ct.ThrowIfCancellationRequested();
            var report = parameters.SystemPrompt.Contains("請產生一份"); var analysis = parameters.SystemPrompt.Contains("一個區段");
            if (report && FailReportOnce) { FailReportOnce = false; throw new HttpRequestException("Synthetic final report failure."); }
            var files = Regex.Matches(prompt, @"file-\d\.cs").Select(x => x.Value).Distinct().ToArray();
            var output = (report ? "整體結論：" : "分析：") + string.Join(", ", files);
            if ((analysis && VerboseAnalysis) || (!report && !analysis && VerboseReduction)) output += new string('中', 1000);
            yield return new(output);
            yield return new("", true, 123, 6, analysis && TruncateAnalysis ? "MAX_TOKENS" : "STOP");
        }
    }
}
