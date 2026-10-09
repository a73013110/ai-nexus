using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Features.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using AiNexus.Features.Jobs;

namespace AiNexus.Tests;

public sealed class RepositoryReviewTests
{
    private static NexusFactory Factory(FixtureGitea source, ReviewProvider provider) => new(services: services =>
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
        Assert.Equal("completed", detail.Review.Job.Status); Assert.Equal(purpose, detail.Review.Purpose); Assert.Equal(3, detail.Version);
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

    [Theory]
    [InlineData("english")]
    [InlineData("oversized")]
    [InlineData("truncated")]
    public async Task InvalidBriefGetsOneCorrectionAndNeverPublishesAPartialReport(string failure)
    {
        var provider = new ReviewProvider { InvalidReport = failure };
        await using var factory = Factory(new() { Diff = Diff(2, 10) }, provider); using var client = await factory.SignedInAsync();
        var review = await Create(client); await Process(factory);
        var detail = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.Equal("completed", detail.Review.Job.Status); Assert.NotNull(detail.Report); Assert.False(detail.Report.Truncated);
        Assert.Equal(2, provider.Calls.Count); Assert.Contains("繁體中文", provider.Calls[0].Parameters.SystemPrompt);
        Assert.Contains("上一份輸出未通過", provider.Calls[1].Prompt);
        Assert.Contains("跨檔案變更", detail.Report.Output); Assert.True(detail.Report.Output.Length < 600);
        Assert.Equal(246, detail.Report.InputTokens); Assert.Equal(12, detail.Report.OutputTokens);
    }

    [Fact]
    public async Task RepeatedInvalidOutputFailsWithoutSavingABrokenFinalReport()
    {
        var provider = new ReviewProvider { InvalidReport = "truncated", AlwaysInvalid = true };
        await using var factory = Factory(new() { Diff = Diff(2, 10) }, provider); using var client = await factory.SignedInAsync();
        var review = await Create(client); await Process(factory);
        var detail = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.Equal("failed", detail.Review.Job.Status); Assert.Equal("review_brief_invalid", detail.Review.Job.ErrorCode);
        Assert.Null(detail.Report); Assert.Equal(2, provider.Calls.Count);
    }

    [Fact]
    public void ManySmallFilesShareAnalysisCallsWithoutLosingSource()
    {
        var diff = Diff(20, 100);
        var snapshot = RepositoryReviewPlan.Create(diff, new() { ContextTokens = 12000, MaxOutputTokens = 10000 }, "team/repo", new string('a', 40), null, "", "review");
        Assert.True(snapshot.Slices.Length < 20); Assert.Equal(diff, RepositoryReviewPlan.Source(snapshot.Slices));
        Assert.All(snapshot.Slices, x => Assert.InRange(Encoding.UTF8.GetByteCount(x.Diff), 1, snapshot.InputBudget));
    }

    [Fact]
    public void MaximumFocusNoteStillLeavesRoomForFramingAndCorrection()
    {
        var note = new string('x', 2000); var head = new string('a', 40);
        var snapshot = RepositoryReviewPlan.Create(Diff(3, 9000), new() { ContextTokens = 32768, MaxOutputTokens = 4096 }, "team/repo", head, null, note, "review");
        var context = RepositoryReviewPlan.Context("team/repo", head, null, note);
        Assert.All(snapshot.Slices, slice => Assert.True(context.Length + slice.Label.Length + slice.Diff.Length + 1024 < ModelTaskService.MaxPromptCharacters));
        Assert.Equal(Diff(3, 9000), RepositoryReviewPlan.Source(snapshot.Slices));
    }

    [Theory]
    [InlineData("{\"conclusion\":\"No issues\",\"changes\":[],\"findings\":[],\"limitation\":\"\"}")]
    [InlineData("{\"conclusion\":\"未發現明確缺陷\",\"changes\":[],\"findings\":[]}")]
    [InlineData("{\"conclusion\":\"結論\",\"changes\":[\"一\",\"二\",\"三\",\"四\"],\"findings\":[],\"limitation\":\"\"}")]
    public void BriefRejectsEnglishIncompleteAndExcessiveResponses(string json) =>
        Assert.False(RepositoryReviewBrief.TryRender(json, "review", out _));

    [Fact]
    public async Task BoundedModelTasksReserveTheRequestedOutputInsteadOfTheFullModelLimit()
    {
        await using var factory = new NexusFactory(inference: o => o.Models[0].MaxOutputTokens = 7000);
        using var client = await factory.SignedInAsync(); using var scope = factory.Services.CreateScope();
        var owner = await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.Select(x => x.Id).SingleAsync();
        var result = await scope.ServiceProvider.GetRequiredService<ModelTaskService>().GenerateAsync(owner, "test-task", new string('x', 4000), "精簡回答。", CancellationToken.None, maxOutputTokens: 128);
        Assert.False(result.Value!.Truncated); Assert.Equal(128, factory.Provider.LastParameters!.MaxOutputTokens);
    }

    [Fact]
    public async Task VersionTwoReportsStillUseTheirFrozenMarkdownInstruction()
    {
        var provider = new ReviewProvider(); await using var factory = Factory(new() { Diff = Diff(2, 10) }, provider);
        using var client = await factory.SignedInAsync(); var review = await Create(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var row = await db.Set<RepositoryReview>().SingleAsync(x => x.Id == review.Id);
            var snapshot = RepositoryReviewService.Snapshot(row);
            row.SnapshotJson = JsonSerializer.Serialize(snapshot with { Version = 2, ReportInstruction = "請產生一份舊版固定 Markdown 報告。" });
            await db.SaveChangesAsync();
        }
        await Process(factory);
        var detail = (await client.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.Equal("completed", detail.Review.Job.Status); Assert.Equal(2, detail.Version); Assert.NotNull(detail.Report);
        Assert.Equal("請產生一份舊版固定 Markdown 報告。", Assert.Single(provider.Calls).Parameters.SystemPrompt);
    }

    private sealed class ReviewProvider : IInferenceProvider
    {
        public List<(string Prompt, GenerationParameters Parameters)> Calls { get; } = [];
        public bool VerboseAnalysis { get; init; }
        public bool VerboseReduction { get; set; }
        public bool FailReportOnce { get; set; }
        public bool TruncateAnalysis { get; init; }
        public string? InvalidReport { get; set; }
        public bool AlwaysInvalid { get; init; }
        public Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "test-model" });
        public async IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, [EnumeratorCancellation] CancellationToken ct)
        {
            // Like Ollama, read the instruction only from the message list rather than from provider-specific parameters.
            var system = messages.Single(x => x.Role == "system").Content;
            var prompt = messages.Single(x => x.Role == "user").Content; Calls.Add((prompt, parameters)); await Task.Yield(); ct.ThrowIfCancellationRequested();
            var report = system.Contains("請產生一份"); var analysis = system.Contains("一個區段");
            if (report && FailReportOnce) { FailReportOnce = false; throw new HttpRequestException("Synthetic final report failure."); }
            var files = Regex.Matches(prompt, @"file-\d\.cs").Select(x => x.Value).Distinct().ToArray();
            var output = (report ? "整體結論：" : "分析：") + string.Join(", ", files);
            var truncated = analysis && TruncateAnalysis;
            if (report && system.Contains("JSON"))
            {
                output = JsonSerializer.Serialize(new { conclusion = "已彙整跨檔案變更，未發現明確缺陷。", changes = files.Take(3).Select(x => "變更包含 " + x).ToArray(), findings = Array.Empty<object>(), limitation = "" });
                if (InvalidReport is { } failure)
                {
                    output = failure is "english" or "oversized"
                        ? JsonSerializer.Serialize(new { conclusion = failure == "english" ? "No issues found" : new string('中', 500), changes = Array.Empty<string>(), findings = Array.Empty<object>(), limitation = "" })
                        : "{\"conclusion\":\"未完成";
                    truncated = failure == "truncated";
                    if (!AlwaysInvalid) InvalidReport = null;
                }
            }
            if ((analysis && VerboseAnalysis) || (!report && !analysis && VerboseReduction)) output += new string('中', 1000);
            yield return new(output);
            yield return new("", true, 123, 6, truncated ? "MAX_TOKENS" : "STOP");
        }
    }
}
