using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Admin.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Operations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Work.Conversations;
using AiNexus.Platform.Auditing;

namespace AiNexus.IntegrationTests.Audit;

public sealed class ListActivityAuditTests
{
    [Fact]
    public async Task AuditorsCanInvestigateWithoutManagingAccountsAndRevocationAppliesImmediately()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        using var auditor = await factory.SignedInAsync("bob");
        var target = (await auditor.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/audit/catalog")).StatusCode);
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/auditors", new GroupUpdateRequest("稽核查閱", true, ["audit"], Scopes: new Dictionary<string, string> { ["audit"] = "all" }))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/roles/auditor", new RoleUpdateRequest("稽核人員", true, ["auditors"]))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/roles", new UserRolesRequest(["member", "auditor"]))).EnsureSuccessStatusCode();
        var me = (await auditor.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Contains(me.Access.Features, feature => feature.Id == "audit" && feature.Route == "/admin/audit");
        Assert.DoesNotContain(me.Access.Features, feature => feature.Id == "identity.users");
        var catalog = (await auditor.GetFromJsonAsync<AuditCatalogDto>("/api/v1/admin/audit/catalog"))!;
        Assert.Contains(catalog.Features, feature => feature.Id == "audit" && feature.Route == "/admin/audit");
        Assert.NotEmpty(catalog.Models);
        // 列出所有功能（而不只限稽核者自己的授權），順序是目錄的宣告順序：紀錄的變更可能指向任何功能。
        Assert.Equal(FeatureCatalog.Grants.Select(x => x.Id), catalog.Features.Select(x => x.Id));
        // 功能的選單位置來自完整的預設樹，不依稽核者自己的授權修剪：檢索管理在系統管理的子分群底下，動作接在上層頁面之後。
        var data = Assert.Single(catalog.NavigationSections, x => x.Id == "system.data");
        Assert.Equal(["系統管理", "資料與檢索"], data.Path);
        Assert.Contains(FeatureCatalog.KnowledgeRetrieval, data.FeatureIds);
        var observability = Assert.Single(catalog.NavigationSections, x => x.Id == "system.observability");
        var logs = observability.FeatureIds.SkipWhile(x => x != FeatureCatalog.DiagnosticsLogs).Take(3);
        Assert.Equal([FeatureCatalog.DiagnosticsLogs, FeatureCatalog.DiagnosticsLogsDetail, FeatureCatalog.DiagnosticsLogsExport], logs);
        var rows = (await auditor.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?category=administration"))!;
        Assert.Contains(rows, row => row.Action == IdentityAudit.UserRoleUpdated.Name && row.ResourceId == target.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/roles", new UserRolesRequest(["administrator"]))).StatusCode);
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/auditors", new GroupUpdateRequest("稽核查閱", true, []))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/audit/catalog")).StatusCode);
    }

    [Fact]
    public async Task CategoryFilterUsesTheStoredCategoryAndTraceFiltersKeepPagingAndAuthorization()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); using var member = await factory.SignedInAsync("bob");
        var owner = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        const string trace = "1234567890abcdef1234567890abcdef";
        // 分類存在欄位上，不從動作名稱推：名稱看起來像別的分類也照存的值篩。
        await SeedAsync(factory, AuditCategories.Values.SelectMany(category => new[] { "fixture.one.created", "fixture.two.created" }
            .Select(action => new AuditEvent { Category = category, OwnerId = owner, Action = action, TraceId = trace })));
        foreach (var category in AuditCategories.Values)
        {
            var rows = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category={category}&traceId={trace}&result=success"))!;
            Assert.Equal(2, rows.Length); Assert.All(rows, row => { Assert.Equal(category, row.Category); Assert.Equal(trace, row.TraceId); });
            var page = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category={category}&traceId={trace}&before={rows[0].Id}"))!;
            Assert.Equal(rows[1].Id, Assert.Single(page).Id);
        }
        var conversation = await ChatApi.CreateConversation(member);
        var operations = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category=activity&result=success&search={conversation.Id}"))!;
        var created = Assert.Single(operations);
        Assert.Equal((ConversationsAudit.ConversationCreated.Name, AuditEvent.Completed), (created.Action, created.Result));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit?category=unsupported")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit?traceId=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/v1/admin/audit?category=authentication&traceId={trace}")).StatusCode);
    }

    [Fact]
    public async Task ResultFilterSplitsCompletedFromErrorCodes()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var user = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        await SeedAsync(factory, new[] { AuditEvent.Completed, AuditEvent.Completed, "executor_lost", "price_exists" }
            .Select(result => new AuditEvent { Category = "activity", OwnerId = user.Id, Action = "fixture.lifecycle.finished", Result = result }));
        var failures = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.&result=failed"))!;
        Assert.Equal(["price_exists", "executor_lost"], failures.Select(x => x.Result));
        Assert.Equal(2, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.&result=success"))!.Length);
        // 多個結果條件符合任一即可；群組與精確結果可以混用，同樣是符合任一。
        var chosen = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.&result=executor_lost&result=price_exists"))!;
        Assert.Equal(["price_exists", "executor_lost"], chosen.Select(x => x.Result));
        var mixed = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.&result=success&result=executor_lost"))!;
        Assert.Equal(["executor_lost", AuditEvent.Completed, AuditEvent.Completed], mixed.Select(x => x.Result));
        Assert.Equal(4, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.&result=failed&result=success"))!.Length);
    }

    [Fact]
    public async Task PageSizeIsOneOfTheOfferedSizes()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var user = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        await SeedAsync(factory, Enumerable.Range(0, 60).Select(_ => new AuditEvent { Category = "activity", OwnerId = user.Id, Action = "fixture.page.created" }));
        Assert.Equal(25, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.page&take=25"))!.Length);
        var first = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.page&take=50"))!;
        Assert.Equal(50, first.Length);
        var next = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?action=fixture.page&take=50&before={first[^1].Id}"))!;
        Assert.Equal(10, next.Length); Assert.Empty(first.Select(x => x.Id).Intersect(next.Select(x => x.Id)));
        Assert.Equal(60, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.page"))!.Length);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit?take=30")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit?take=1000")).StatusCode);
    }

    [Fact]
    public async Task CatalogListsDeclaredActionsWithTheirCategoryAndModule()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var catalog = (await admin.GetFromJsonAsync<AuditCatalogDto>("/api/v1/admin/audit/catalog"))!;
        Assert.Equal(AuditCategories.Values, catalog.Categories.Select(x => x.Value));
        Assert.Equal(catalog.Actions.Count, catalog.Actions.Select(x => x.Value).Distinct().Count());
        Assert.All(catalog.Actions, action =>
        {
            Assert.NotEmpty(action.Label);
            Assert.Contains(action.Category, AuditCategories.Values);
            Assert.StartsWith(action.Module + ".", action.Value, StringComparison.Ordinal);
            Assert.Contains(catalog.Modules, x => x.Value == action.Module);
        });
        Assert.Equal(("access", "attendance"), Pick(AttendanceAudit.RecordRead));
        Assert.Equal(("authentication", "identity"), Pick(IdentityAudit.SignedIn));
        Assert.Equal(("administration", "access-control"), Pick(AccessControlAudit.RoleUpdated));
        // 模組依功能目錄排序，標籤取目錄的模組名稱；平台模組沒有頁面，用自己的標籤。
        Assert.Equal("出勤", Assert.Single(catalog.Modules, x => x.Value == "attendance").Label);
        Assert.Equal("網路搜尋", Assert.Single(catalog.Modules, x => x.Value == "web-search").Label);

        (string, string) Pick(AuditAction action)
        {
            var dto = Assert.Single(catalog.Actions, x => x.Value == action.Name);
            return (dto.Category, dto.Module);
        }
    }

    [Fact]
    public async Task ExportHoldsTheWholeQueryIsAuditedAndRefusesMoreThanTheLimit()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: services => services.Configure<AuditOptions>(x => x.MaxExportRows = 3));
        using var admin = await factory.SignedInAsync(); using var member = await factory.SignedInAsync("bob");
        var user = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        await SeedAsync(factory, [
            new AuditEvent { Category = "activity", OwnerId = user.Id, Action = "fixture.export.created", DetailsJson = """{"resourceKey":"=HYPERLINK(\"evil\")"}""" },
            new AuditEvent { Category = "activity", OwnerId = user.Id, Action = "fixture.export.created", Result = "fixture_failed" },
            .. Enumerable.Range(0, 4).Select(_ => new AuditEvent { Category = "activity", OwnerId = user.Id, Action = "fixture.overflow.created" })]);
        using var response = await admin.GetAsync("/api/v1/admin/audit/export?action=fixture.export.&result=success&result=fixture_failed");
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lines = System.Text.Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("\"紀錄 ID\",\"時間（UTC）\",\"操作者\"", lines[0]);
        Assert.Equal(3, lines.Length);
        // 依時間由新到舊排序；每個儲存格中類似公式的文字都會被中和。
        Assert.Contains("\"fixture_failed\"", lines[1]);
        Assert.Contains("\"'=HYPERLINK(\"\"evil\"\")\"", lines[2]);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var record = Assert.Single(db.AuditEvents.AsNoTracking().Where(x => x.Action == AuditAudit.EventExported.Name));
            Assert.Equal((user.Id, AuditEvent.Completed, "access", FeatureCatalog.Audit), (record.OwnerId, record.Result, record.Category, record.FeatureId));
            Assert.Contains("\"action\":\"fixture.export.\"", record.DetailsJson); Assert.Contains("\"result\":[\"success\",\"fixture_failed\"]", record.DetailsJson);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit/export?action=fixture.overflow.")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            // 超過上限被拒的嘗試同樣留有紀錄。
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refused = Assert.Single(db.AuditEvents.AsNoTracking().Where(x => x.Action == AuditAudit.EventExported.Name && x.DetailsJson!.Contains("fixture.overflow")));
            Assert.NotEqual(AuditEvent.Completed, refused.Result);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/admin/audit/export?action=fixture.export.")).StatusCode);
    }

    [Fact]
    public async Task ExportCombinesResultGroupsWithAnActionPrefix()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var user = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        await SeedAsync(factory, [
            new AuditEvent { Category = "activity", OwnerId = user.Id, Action = "fixture.multi.created" },
            new AuditEvent { Category = "activity", OwnerId = user.Id, Action = "fixture.multi.updated", Result = "fixture_failed" },
            new AuditEvent { Category = "activity", OwnerId = user.Id, Action = "fixture.multi.deleted", Result = "feature_forbidden" },
            new AuditEvent { Category = "activity", OwnerId = user.Id, Action = "fixture.other.created", Result = "fixture_failed" }]);
        async Task<string[]> ActionsAsync(string query)
        {
            var text = (await admin.GetStringAsync("/api/v1/admin/audit/export?" + query)).TrimStart('\uFEFF');
            return text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(line => line.Split("\",\"")[4]).Order().ToArray();
        }
        // 群組與精確結果混用時符合任一即可；動作前綴排除不相干的 fixture.other。
        Assert.Equal(["fixture.multi.created", "fixture.multi.updated"], await ActionsAsync("action=fixture.multi.&result=success&result=fixture_failed"));
        Assert.Equal(["fixture.multi.deleted", "fixture.multi.updated"], await ActionsAsync("action=fixture.multi.&result=failed"));
    }

    [Fact]
    public async Task ExportIsRateLimitedPerUser()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++) statuses.Add((await admin.GetAsync("/api/v1/admin/audit/export?action=fixture.none.")).StatusCode);
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    private static async Task SeedAsync(AppFactory factory, IEnumerable<AuditEvent> events)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AuditEvents.AddRange(events);
        await db.SaveChangesAsync();
    }
}
