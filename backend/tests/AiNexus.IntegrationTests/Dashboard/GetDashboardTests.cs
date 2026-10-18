using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Attachments;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Knowledge;
using AiNexus.Data.App.Notifications;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Account;
using AiNexus.Features.Dashboard;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Dashboard;

public sealed class GetDashboardTests
{
    private const string Period = "from=2020-01-01T00:00:00Z&until=2020-01-02T00:00:00Z";

    [Fact]
    public async Task SavedOriginalsAreSeparateFromKnowledgeDocumentsAndRespectOwnerScope()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var a = await Me(alice); var b = await Me(bob);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var original = new Attachment { Size = 10, OwnerId = a, FileName = "shared-original.txt", ContentType = "text/plain", InLibrary = true };
            db.AddRange(original, new Attachment { Size = 5, OwnerId = a, ContentType = "image/png", InLibrary = true },
                new Attachment { Size = 1, OwnerId = a, InLibrary = false },
                new Attachment { Size = 1, OwnerId = a, InLibrary = true, StorageState = AttachmentStates.Pending },
                new Attachment { Size = 1, OwnerId = a, InLibrary = true, StorageState = AttachmentStates.Deleting },
                new Attachment { Size = 1, OwnerId = b, InLibrary = true });
            var collection = new Resource { OwnerId = a, Kind = "knowledge", Name = "規章" };
            db.AddRange(collection, new Collection { Id = collection.Id });
            // 兩個獨立處理的來源共用同一份原始檔；只有放進知識庫集合的那一個才會成為知識文件。
            foreach (var (status, inCollection) in new[] { ("ready", true), ("failed", true), ("ready", false) })
            {
                var resource = new Resource { OwnerId = a, Kind = "document", Name = "processed-source" };
                db.Add(resource);
                db.Add(new Document { Id = resource.Id, AttachmentId = original.Id, Status = status, CollectionId = inCollection ? collection.Id : null });
            }
            await db.SaveChangesAsync();
        }
        // 目前的計數不受統計區間影響；此處的統計區間設在過去，因此不影響結果。
        var personal = await Read(alice, Period);
        Assert.Equal(2, Value(personal, "files", "files"));
        Assert.Equal(15, Value(personal, "files", "size"));
        Assert.Equal(2, Value(personal, "knowledge", "documents"));
        Assert.Equal(1, Value(personal, "knowledge", "failed"));
        Assert.Equal(1, Value(personal, "knowledge", "collections"));
        Assert.Equal(["圖片", "文字"], Section(personal, "files").Shares.Select(x => x.Label).Order(StringComparer.Ordinal));
        Assert.Equal(1, Value(await Read(bob, Period), "files", "files"));
        Assert.Equal(0, Value(await Read(bob, Period), "knowledge", "documents"));
        Assert.Equal(3, Value(await Read(alice, "scope=platform&" + Period), "files", "files"));
        var selected = await Read(alice, $"scope=platform&ownerId={b}&{Period}");
        Assert.Equal(b, selected.OwnerId);
        Assert.Equal(1, Value(selected, "files", "files"));
    }

    [Fact]
    public async Task SectionsFollowFeatureGrantsAndAreTheSameInEveryScope()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var personal = await Read(alice, Period);
        Assert.Contains(personal.Sections, x => x.Id == "chat");
        // 區段標題取自功能的名稱與路由（名稱可能已被客製化）；明確指定的標題則保留不變。
        Assert.Equal(("對話", "/chat"), (Section(personal, "chat").Title, Section(personal, "chat").Route));
        Assert.Equal(("通知", ""), (Section(personal, "notifications").Title, Section(personal, "notifications").Route));
        var platform = await Read(alice, "scope=platform&" + Period);
        // 總覽對所有人的區段結構都相同：平台範圍只會擴大數字，不會改變區段。
        Assert.DoesNotContain(platform.Sections, x => x.Id is "platform" or "monitoring");
        Assert.Equal(personal.Sections.Select(x => x.Id), platform.Sections.Select(x => x.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/dashboard?scope=platform&" + Period)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync($"/api/v1/dashboard?ownerId={Guid.NewGuid()}&{Period}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<RoleGroupFeature>()
                .Where(x => x.FeatureId == FeatureIds.Knowledge).ExecuteDeleteAsync();
        Assert.DoesNotContain((await Read(bob, Period)).Sections, x => x.Id == "knowledge");
    }

    [Fact]
    public async Task PlatformScopeFollowsPlatformOperationsNotAccessManagement()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var b = await Me(bob);
        await PlatformGrants.SplitOperationsAsync(factory, b);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/v1/dashboard?scope=platform&" + Period)).StatusCode);
        (await alice.GetAsync("/api/v1/dashboard?" + Period)).EnsureSuccessStatusCode();
        Assert.Equal("platform", (await Read(bob, "scope=platform&" + Period)).Scope);
        using var scope = factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEvents.AnyAsync(x => x.Action == "dashboard.platform.read" && x.OwnerId == b));
    }

    [Fact]
    public async Task QuestionsAreBucketedByTheViewersDayAndRecentTitlesAreAlwaysTheViewers()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var a = await Me(alice); var b = await Me(bob);
        var evening = DateTimeOffset.Parse("2026-09-30T18:00:00Z", CultureInfo.InvariantCulture);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mine = new Conversation { OwnerId = a, Title = "我的對話", UpdatedAt = evening };
            var theirs = new Conversation { OwnerId = b, Title = "別人的對話", UpdatedAt = evening.AddHours(1) };
            db.AddRange(mine, theirs,
                new Message { ConversationId = mine.Id, Role = "user", CreatedAt = evening },
                new Message { ConversationId = mine.Id, Role = "assistant", CreatedAt = evening },
                new Message { ConversationId = theirs.Id, Role = "user", CreatedAt = evening });
            await db.SaveChangesAsync();
        }
        const string Window = "from=2026-09-30T00:00:00Z&until=2026-10-02T00:00:00Z&offsetMinutes=480";
        var chat = Section(await Read(alice, Window), "chat");
        Assert.Equal(1, chat.Metrics.Single(x => x.Key == "questions").Value);
        Assert.Equal("2026-10-01", Assert.Single(chat.Series.Single(x => x.Key == "questions").Points).Date);
        var platform = Section(await Read(alice, "scope=platform&" + Window), "chat");
        Assert.Equal(2, platform.Metrics.Single(x => x.Key == "questions").Value);
        Assert.Equal(["我的對話"], platform.Items.Select(x => x.Title));
        Assert.Equal("/chat/", platform.Items[0].Link![..6]);
    }

    [Fact]
    public async Task FailedNotificationsShowTheReviewedMessageInsteadOfStoredText()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync();
        var a = await Me(alice);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(
                new Notification { OwnerId = a, EventKey = "e1", Type = "job", Severity = "error", Title = "內部細節", Body = "secret", IssueCode = "AB12CD34", TargetKind = "task", TargetId = Guid.NewGuid() },
                new Notification { OwnerId = a, EventKey = "e2", Type = "share", Severity = "info", Title = "收到分享", Body = "內容", TargetKind = "share", TargetId = Guid.NewGuid(), ReadAt = DateTimeOffset.UnixEpoch });
            await db.SaveChangesAsync();
        }
        var section = Section(await Read(alice, Period), "notifications");
        Assert.Equal(1, section.Metrics.Single(x => x.Key == "unread").Value);
        Assert.Equal(DashboardStates.Attention, section.State);
        var item = Assert.Single(section.Items);
        Assert.Equal("操作未完成", item.Title);
        Assert.DoesNotContain("secret", item.Detail, StringComparison.Ordinal);
        Assert.StartsWith("/tasks?job=", item.Link, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenReportsIncludeLegacyAndBackgroundUsagePerModelAndRespectTimezoneAndOwner()
    {
        await using var factory = new NexusFactory(); using var owner = await factory.SignedInAsync(); using var other = await factory.SignedInAsync("bob");
        var id = await Me(owner); var otherId = await Me(other);
        var at = DateTimeOffset.Parse("2026-09-30T18:00:00Z", CultureInfo.InvariantCulture);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(new ModelInvocation { OwnerId = id, Kind = "repository-review", ModelId = "test-model", Status = "completed", InputTokens = 120, OutputTokens = 30, CreatedAt = at }, new ModelInvocation { OwnerId = id, Kind = "ocr", ModelId = "another", Status = "failed", CreatedAt = at }, new ModelInvocation { OwnerId = otherId, ModelId = "test-model", InputTokens = 999, OutputTokens = 999, CreatedAt = at }); await db.SaveChangesAsync(); }
        var dashboard = await Read(owner, "scope=personal&from=2026-09-30T00:00:00Z&until=2026-10-02T00:00:00Z&offsetMinutes=480");
        Assert.Equal(480, dashboard.Period.OffsetMinutes);
        Assert.Equal(150, dashboard.Tokens.Daily.Sum(x => x.InputTokens + x.OutputTokens)); Assert.All(dashboard.Tokens.Daily, x => Assert.Equal("2026-10-01", x.Date));
        Assert.Equal(2, dashboard.Tokens.Daily.Sum(x => x.Requests)); Assert.Equal(1, dashboard.Tokens.Daily.Sum(x => x.RequestsWithUsage));
        Assert.Equal("測試模型", dashboard.Tokens.Daily.Single(x => x.ModelId == "test-model").ModelDisplayName);
        Assert.Equal("已停用的模型", dashboard.Tokens.Daily.Single(x => x.ModelId == "another").ModelDisplayName);
    }

    private static async Task<Guid> Me(HttpClient client) => (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;

    private static async Task<DashboardDto> Read(HttpClient client, string query) => (await client.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard?" + query))!;

    private static DashboardSectionDto Section(DashboardDto dashboard, string id) => dashboard.Sections.Single(x => x.Id == id);

    private static double? Value(DashboardDto dashboard, string section, string metric) => Section(dashboard, section).Metrics.Single(x => x.Key == metric).Value;
}
