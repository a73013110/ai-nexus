using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Account;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Dashboard;
using AiNexus.Features.Notifications;
using AiNexus.Features.Repositories;
using AiNexus.Features.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using static AiNexus.Tests.ChatApiTests;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Tests;

public sealed class WorkspaceExperienceTests
{
    private sealed class UnfinishedHandler : IBackgroundJobHandler
    {
        public string Kind => "unfinished-test";
        public Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => Task.FromResult(Result.Success);
        public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
        {
            var user = await execution.Database.Users.SingleAsync(x => x.Id == execution.Job.OwnerId, ct);
            user.DisplayName = "Uncheckpointed mutation";
            throw new InvalidOperationException("Synthetic failure before checkpoint.");
        }
    }
    [Fact]
    public async Task TerminalNotificationDoesNotCommitUncheckpointedHandlerChanges()
    {
        await using var factory = new NexusFactory(services: services => services.AddScoped<IBackgroundJobHandler, UnfinishedHandler>());
        using var client = await factory.SignedInAsync();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(me.Id, null, Guid.NewGuid(), "unfinished-test", "未完成的修改");
            id = job.Id; await scope.ServiceProvider.GetRequiredService<NexusDbContext>().SaveChangesAsync();
        }
        await Process(factory);
        Assert.Equal(me.DisplayName, (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.DisplayName);
        Assert.Equal("failed", (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{id}"))!.Status);
        Assert.Equal("task.failed", Assert.Single((await client.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Items).Type);
    }
    private static async Task<AttachmentDto> Upload(HttpClient client, string name, byte[] bytes)
    {
        using var body = new MultipartFormDataContent(); body.Add(new ByteArrayContent(bytes), "file", name);
        using var response = await client.PostAsync("/api/v1/attachments", body); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AttachmentDto>())!;
    }
    private static async Task Process(NexusFactory factory)
    {
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        while (await worker.ProcessNextAsync(CancellationToken.None)) { }
    }
    [Fact]
    public async Task NotificationPagingHandlesEqualTimestampsAndReadThroughNeverCrossesAccountsOrNewEvents()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var owner = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var other = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            for (var i = 0; i < 55; i++) db.Add(new WorkspaceNotification { OwnerId = owner, EventKey = "test:" + i, Type = "task.completed", Title = "完成", TargetKind = "task", TargetId = Guid.NewGuid(), CreatedAt = now });
            db.Add(new WorkspaceNotification { OwnerId = other, EventKey = "private", Title = "私人", CreatedAt = now }); await db.SaveChangesAsync();
        }
        var page = (await alice.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!;
        Assert.Equal(50, page.Items.Count); Assert.Equal(55, page.Unread); Assert.True(page.HasMore);
        var next = (await alice.GetFromJsonAsync<NotificationPageDto>($"/api/v1/notifications?before={page.Items[^1].Id}"))!;
        Assert.Equal(5, next.Items.Count); Assert.False(next.HasMore); Assert.Equal(55, page.Items.Concat(next.Items).Select(x => x.Id).Distinct().Count());
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync($"/api/v1/notifications?before={page.Items[0].Id}")).StatusCode);
        (await bob.PostAsync($"/api/v1/notifications/{page.Items[0].Id}/read", null)).EnsureSuccessStatusCode();
        (await bob.DeleteAsync($"/api/v1/notifications/{page.Items[0].Id}")).EnsureSuccessStatusCode();
        Assert.Equal(55, (await alice.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Unread);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); db.Add(new WorkspaceNotification { OwnerId = owner, EventKey = "newer", Title = "新通知", CreatedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }
        (await alice.PostAsJsonAsync("/api/v1/notifications/read", new ReadNotificationsRequest(now))).EnsureSuccessStatusCode();
        Assert.Equal(1, (await alice.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications?unread=true"))!.Unread);
        Assert.Equal(1, (await bob.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Unread);
        alice.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsJsonAsync("/api/v1/notifications/read", new ReadNotificationsRequest(now))).StatusCode);
    }
    [Fact]
    public async Task CompletedGenerationsAndSharesPublishDurableDeduplicatedTypedNotifications()
    {
        await using var factory = new NexusFactory(); using var owner = await factory.SignedInAsync(); using var reader = await factory.SignedInAsync("bob");
        var conversation = await CreateConversation(owner); var run = await CreateRun(owner, conversation.Id, "回答完成後通知"); await WaitForTerminal(owner, run.Id);
        var notifications = (await owner.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!;
        var completion = Assert.Single(notifications.Items); Assert.Equal("conversation.completed", completion.Type); Assert.Equal(new("conversation", conversation.Id), completion.Target);
        var user = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var response = await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", conversation.Id, [user])); response.EnsureSuccessStatusCode();
        var share = (await response.Content.ReadFromJsonAsync<ShareDto>())!;
        var received = Assert.Single((await reader.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Items);
        Assert.Equal(new("share", share.Id), received.Target);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        db.ChangeTracker.Clear();
        var row = await db.Set<WorkspaceNotification>().SingleAsync(x => x.Id == completion.Id);
        await scope.ServiceProvider.GetRequiredService<NotificationService>().PublishAsync(row.OwnerId, row.EventKey, row.Type, row.Severity, row.Title, row.Body, row.TargetKind, row.TargetId, CancellationToken.None);
        await db.SaveChangesAsync(); Assert.Equal(2, await db.Set<WorkspaceNotification>().CountAsync());
    }
    [Fact]
    public async Task RenamePreservesOriginalBytesAndFrozenShareNamesAndRequiresExpectedName()
    {
        await using var factory = new NexusFactory(); using var owner = await factory.SignedInAsync(); using var reader = await factory.SignedInAsync("bob");
        var file = await Upload(owner, "原始.txt", Encoding.UTF8.GetBytes("附件原始文字")); var conversation = await CreateConversation(owner);
        var runResponse = await PostRun(owner, new(conversation.Id, "test-model", "閱讀", null, null, AttachmentIds: [file.Id])); runResponse.EnsureSuccessStatusCode(); await WaitForTerminal(owner, (await runResponse.Content.ReadFromJsonAsync<RunDto>())!.Id);
        var readerId = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var shareResponse = await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", conversation.Id, [readerId], IncludeAttachments: true)); shareResponse.EnsureSuccessStatusCode(); var share = (await shareResponse.Content.ReadFromJsonAsync<ShareDto>())!;
        var rename = await owner.PutAsJsonAsync($"/api/v1/files/{file.Id}/name", new RenameLibraryFileRequest("新名稱.txt", file.FileName)); rename.EnsureSuccessStatusCode();
        Assert.Equal("新名稱.txt", (await rename.Content.ReadFromJsonAsync<AttachmentDto>())!.FileName);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/v1/files/{file.Id}/name", new RenameLibraryFileRequest("衝突.txt", file.FileName))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/v1/files/{file.Id}/name", new RenameLibraryFileRequest("執行.exe", "新名稱.txt"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.PutAsJsonAsync($"/api/v1/files/{file.Id}/name", new RenameLibraryFileRequest("偷改.txt", "新名稱.txt"))).StatusCode);
        var preview = (await reader.GetFromJsonAsync<SharedFilePreviewDto>($"/api/v1/shares/{share.Id}/files/{file.Id}/preview"))!;
        Assert.Equal(file.FileName, preview.File.FileName); Assert.Contains("附件原始文字", Assert.Single(preview.Pages).Text);
        using var inline = await reader.GetAsync($"/api/v1/shares/{share.Id}/files/{file.Id}"); inline.EnsureSuccessStatusCode(); Assert.Equal("attachment", inline.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("附件原始文字", await inline.Content.ReadAsStringAsync());
        var snapshot = (await reader.GetFromJsonAsync<SharedContentDto>($"/api/v1/shares/{share.Id}"))!;
        Assert.NotNull(snapshot.Snapshot.Messages[^1].Timing); Assert.Equal(123, snapshot.Snapshot.Messages[^1].Timing!.InputTokens);
        (await owner.DeleteAsync($"/api/v1/shares/{share.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/shares/{share.Id}/files/{file.Id}/preview")).StatusCode);
    }
    [Fact]
    public async Task TextBodyLimitAcceptsEscapedChineseAndValidatesTheActualCharacterLimit()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync();
        var collection = (await (await client.PostAsJsonAsync("/api/v1/knowledge/collections", new CollectionRequest("長篇筆記", ""))).Content.ReadFromJsonAsync<CollectionDto>())!;
        var text = new string('字', 20000);
        var response = await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/text", new TextDocumentRequest("長篇文字", text)); response.EnsureSuccessStatusCode();
        var document = (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal(text, (await client.GetFromJsonAsync<TextDocumentDto>($"/api/v1/documents/{document.Id}/text"))!.Text);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/text", new TextDocumentRequest("超長文字", new string('x', 64001)))).StatusCode);
    }
    [Fact]
    public async Task EditableTextUsesCollectionAclVersionChecksAndRebuildsIndexWithANewOriginal()
    {
        await using var factory = new NexusFactory(); using var owner = await factory.SignedInAsync(); using var reader = await factory.SignedInAsync("bob");
        var created = await owner.PostAsJsonAsync("/api/v1/knowledge/collections", new CollectionRequest("文字來源", "")); created.EnsureSuccessStatusCode(); var collection = (await created.Content.ReadFromJsonAsync<CollectionDto>())!;
        var response = await owner.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/text", new TextDocumentRequest("文字筆記", "版本一的原始文字")); response.EnsureSuccessStatusCode(); var document = (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal(1, document.TextVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/v1/documents/{document.Id}/text", new TextDocumentRequest("文字筆記", "先等待索引", 1))).StatusCode);
        await Process(factory);
        var readerId = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        (await owner.PutAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/access", new ResourceAclRequest([new(readerId, "viewer")], []))).EnsureSuccessStatusCode();
        Assert.Equal("版本一的原始文字", (await reader.GetFromJsonAsync<TextDocumentDto>($"/api/v1/documents/{document.Id}/text"))!.Text);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync($"/api/v1/documents/{document.Id}/text", new TextDocumentRequest("篡改", "私人修改", 1))).StatusCode);
        var update = await owner.PutAsJsonAsync($"/api/v1/documents/{document.Id}/text", new TextDocumentRequest("更新筆記", "版本二可檢索的內容", 1)); update.EnsureSuccessStatusCode(); Assert.Equal(2, (await update.Content.ReadFromJsonAsync<DocumentDto>())!.TextVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/v1/documents/{document.Id}/text", new TextDocumentRequest("衝突", "另一人的內容", 1))).StatusCode);
        await Process(factory);
        var pages = (await owner.GetFromJsonAsync<DocumentPageDto[]>($"/api/v1/documents/{document.Id}/pages"))!; Assert.Contains("版本二", Assert.Single(pages).Text);
        var originals = (await owner.GetFromJsonAsync<FileLibraryPageDto>("/api/v1/files"))!.Items;
        Assert.Equal(2, originals.Count); Assert.Contains(originals, x => x.File.FileName == "文字筆記.txt"); Assert.Contains(originals, x => x.File.FileName == "更新筆記.txt");
    }
    [Fact]
    public async Task TokenReportsIncludeLegacyAndBackgroundUsagePerModelAndRespectTimezoneAndOwner()
    {
        await using var factory = new NexusFactory(); using var owner = await factory.SignedInAsync(); using var other = await factory.SignedInAsync("bob");
        var id = (await owner.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id; var otherId = (await other.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var at = DateTimeOffset.Parse("2026-09-30T18:00:00Z", CultureInfo.InvariantCulture);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.AddRange(new ModelInvocation { OwnerId = id, Kind = "repository-review", ModelId = "test-model", Status = "completed", InputTokens = 120, OutputTokens = 30, CreatedAt = at }, new ModelInvocation { OwnerId = id, Kind = "ocr", ModelId = "another", Status = "failed", CreatedAt = at }, new ModelInvocation { OwnerId = otherId, ModelId = "test-model", InputTokens = 999, OutputTokens = 999, CreatedAt = at }); await db.SaveChangesAsync(); }
        var dashboard = (await owner.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard?scope=personal&from=2026-09-30T00:00:00Z&until=2026-10-02T00:00:00Z&offset=480"))!;
        Assert.NotNull(dashboard.Tokens); Assert.Equal(150, dashboard.Tokens!.Daily.Sum(x => x.InputTokens + x.OutputTokens)); Assert.All(dashboard.Tokens.Daily, x => Assert.Equal("2026-10-01", x.Date));
        Assert.Equal(2, dashboard.Tokens.Daily.Sum(x => x.Requests)); Assert.Equal(1, dashboard.Tokens.Daily.Sum(x => x.RequestsWithUsage));
        Assert.Equal("測試模型", dashboard.Tokens.Daily.Single(x => x.ModelId == "test-model").ModelDisplayName);
        Assert.Equal("已停用的模型", dashboard.Tokens.Daily.Single(x => x.ModelId == "another").ModelDisplayName);
    }
    [Fact]
    public async Task ReviewRangesArePinnedIdempotentAndCheckpointRetriesOnlyUnfinishedSections()
    {
        var source = new FixtureGitea { Diff = "diff --git a/a.cs b/a.cs\n@@ -1 +1 @@\n-a\n+" + new string('x', 6000) + "\ndiff --git a/b.cs b/b.cs\n@@ -1 +1 @@\n-c\n+d\n" };
        await using var factory = new NexusFactory(services: services => { services.RemoveAll<IGiteaClient>(); services.AddSingleton<IGiteaClient>(source); services.PostConfigure<GiteaOptions>(o => o.Enabled = true); });
        using var owner = await factory.SignedInAsync(); using var other = await factory.SignedInAsync("bob");
        (await owner.PostAsJsonAsync("/api/v1/repositories/connection", new ConnectRepositoryRequest("fixtureOnlyReadTokenForGitea00001"))).EnsureSuccessStatusCode();
        var request = new CreateRepositoryReviewRequest("hanglong/nexus", new string('a', 40), new string('b', 40), "test-model", "權限", Guid.NewGuid().ToString());
        var response = await owner.PostAsJsonAsync("/api/v1/repositories/reviews", request); response.EnsureSuccessStatusCode(); var review = (await response.Content.ReadFromJsonAsync<RepositoryReviewDto>())!;
        Assert.Equal("queued", review.Job.Status); Assert.Equal("repository-review", review.Job.Kind);
        var again = await owner.PostAsJsonAsync("/api/v1/repositories/reviews", request); again.EnsureSuccessStatusCode(); Assert.Equal(review.Id, (await again.Content.ReadFromJsonAsync<RepositoryReviewDto>())!.Id);
        Assert.Contains(source.Requests, x => x.EndsWith("/compare/" + request.BaseCommit + ".." + request.Commit + "?output=diff", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/v1/repositories/reviews", request with { Note = "不同重點" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/repositories/reviews/{review.Id}")).StatusCode);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Add(new RepositoryReviewResult { ReviewId = review.Id, Ordinal = 0, Output = "已完成區段" });
            await db.Set<BackgroundJob>().Where(x => x.Id == review.Job.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, "failed").SetProperty(x => x.ActiveKey, (string?)null)); await db.SaveChangesAsync(); }
        (await owner.PostAsync($"/api/v1/repositories/reviews/{review.Id}/retry", null)).EnsureSuccessStatusCode(); await Process(factory);
        var detail = (await owner.GetFromJsonAsync<RepositoryReviewDetailDto>($"/api/v1/repositories/reviews/{review.Id}"))!;
        Assert.Equal("completed", detail.Review.Job.Status); Assert.True(detail.Sections.Count > 1); Assert.Equal("已完成區段", detail.Sections[0].Output); Assert.Equal(detail.Sections.Count, factory.Provider.Calls);
        Assert.All(detail.Sections, x => Assert.NotNull(x.Output));
        Assert.NotNull(detail.Report); Assert.Equal(3, detail.Version); Assert.Equal(detail.Sections.Count + 1, detail.Review.Job.TotalUnits);
        Assert.Contains((await owner.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Items, x => x.Target == new NotificationTargetDto("repository-review", review.Id));
        source.Revoked = true;
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/v1/repositories/reviews/{review.Id}")).StatusCode);
    }
    [Fact]
    public void DiffSplittingPreservesAllTextAndBoundsLargeOrBinaryChanges()
    {
        var diff = "diff --git a/long.cs b/long.cs\n@@ -1 +1 @@\n+" + new string('x', 8000) + "\n";
        var slices = RepositoryReviewService.Split(diff, 512).Value!; Assert.Equal(diff, string.Concat(slices.Select(x => x.Diff))); Assert.All(slices, x => Assert.InRange(x.Diff.Length, 1, 512));
        Assert.True(Assert.Single(RepositoryReviewService.Split("diff --git a/a.png b/a.png\nBinary files a/a.png and b/a.png differ\n", 1000).Value!).Binary);
        Assert.Equal("review_no_changes", RepositoryReviewService.Split("", 1000).Error?.Code);
        Assert.Equal("repository_diff_limit", RepositoryReviewService.Split("diff --git a/a b/a\n" + new string('x', 257000), 12000).Error?.Code);
    }
    [Fact]
    public void OfficeExtractionReadsWorksheetsCachedFormulasAndSlidesAndRejectsMacrosOrXmlEntities()
    {
        var extractor = new DocumentExtractor(Options.Create(new AttachmentOptions()));
        var types = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\" />";
        var sheet = "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\"><f>1+1</f><v>2</v></c><c r=\"C1\"><f>SUM(A1:B1)</f></c></row></sheetData></worksheet>";
        var bytes = Zip(new() { ["[Content_Types].xml"] = types, ["xl/workbook.xml"] = "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"成本\" r:id=\"rId1\" /></sheets></workbook>", ["xl/_rels/workbook.xml.rels"] = "<Relationships><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\" /></Relationships>", ["xl/sharedStrings.xml"] = "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>季度目標</t></si></sst>", ["xl/worksheets/sheet1.xml"] = sheet });
        var (type, text) = extractor.Extract("modern.xlsx", bytes, CancellationToken.None).Value; Assert.Contains("spreadsheetml", type); Assert.Contains("工作表：成本", text); Assert.Contains("A1: 季度目標", text); Assert.Contains("B1: 2", text); Assert.Contains("公式沒有已保存", text);
        var ppt = Zip(new() { ["[Content_Types].xml"] = types, ["ppt/slides/slide1.xml"] = "<root xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:p><a:r><a:t>投影片文字</a:t></a:r></a:p></root>" });
        Assert.Contains("投影片文字", extractor.Extract("slides.pptx", ppt, CancellationToken.None).Value.Text);
        var macro = Zip(new() { ["[Content_Types].xml"] = types, ["xl/vbaProject.bin"] = "macro" });
        Assert.Equal("office_macros_unsupported", extractor.Extract("disguised.xlsx", macro, CancellationToken.None).Error?.Code);
        var entity = Zip(new() { ["[Content_Types].xml"] = "<!DOCTYPE Types [<!ENTITY x SYSTEM 'file:///secret'>]><Types>&x;</Types>" });
        Assert.Equal("document_unreadable", extractor.Extract("entity.docx", entity, CancellationToken.None).Error?.Code);
        var bounded = new DocumentExtractor(Options.Create(new AttachmentOptions { MaxExtractedCharacters = 10 }));
        Assert.Equal("document_too_large", bounded.Extract("too-long.xlsx", bytes, CancellationToken.None).Error?.Code);
    }
    private static byte[] Zip(Dictionary<string, string> files)
    {
        using var bytes = new MemoryStream(); using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true)) foreach (var (path, text) in files) { using var writer = new StreamWriter(zip.CreateEntry(path).Open(), Encoding.UTF8); writer.Write(text); }
        return bytes.ToArray();
    }
}
