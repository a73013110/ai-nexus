using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Admin.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Operations;

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
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/auditors", new GroupUpdateRequest("稽核查閱", true, ["audit"]))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/roles/auditor", new RoleUpdateRequest("稽核人員", true, ["auditors"]))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/roles", new UserRolesRequest(["member", "auditor"]))).EnsureSuccessStatusCode();
        var me = (await auditor.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Contains(me.Access.Features, feature => feature.Id == "audit" && feature.Route == "/admin/audit");
        Assert.DoesNotContain(me.Access.Features, feature => feature.Id == "identity.users");
        (await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("reports", "報表", "system", 40))).EnsureSuccessStatusCode();
        var catalog = (await auditor.GetFromJsonAsync<AuditCatalogDto>("/api/v1/admin/audit/catalog"))!;
        Assert.Contains(catalog.Features, feature => feature.Id == "audit" && feature.GroupId == "system");
        Assert.NotEmpty(catalog.Models);
        // 列出所有群組，而不只限稽核者自己選單中的群組：紀錄的變更可能指向任何功能。
        Assert.Equal(["work", "collaboration", "system", "reports"], catalog.NavigationGroups.Select(x => x.Id));
        Assert.Contains(catalog.NavigationGroups, group => group is { Id: "reports", ParentId: "system", Name: "報表" });
        var rows = (await auditor.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?category=administration"))!;
        Assert.Contains(rows, row => row.Action == "admin.user_roles" && row.ResourceId == target.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/roles", new UserRolesRequest(["administrator"]))).StatusCode);
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/auditors", new GroupUpdateRequest("稽核查閱", true, []))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/audit/catalog")).StatusCode);
    }

    [Fact]
    public async Task CategoriesCoverHistoricalActionsAndTraceFiltersKeepPagingAndAuthorization()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); using var member = await factory.SignedInAsync("bob");
        var owner = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        const string trace = "1234567890abcdef1234567890abcdef";
        string[] actions = ["identity.login", "admin.user_roles", "resource.acl.updated", "system.diagnostics.configuration", "billing.price.created",
            "logs.query", "admin.conversation_read", "billing.report.export", "dashboard.platform.read", "integration.record.read", "conversation.created"];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.AddRange(actions.Select(action => new AuditEvent { OwnerId = owner, Action = action, TraceId = trace, Result = action == "billing.report.export" ? "csv" : "saved" }));
            await db.SaveChangesAsync();
        }
        foreach (var category in AuditCategories.Values)
        {
            var rows = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category={category}&traceId={trace}&result=success"))!;
            Assert.NotEmpty(rows); Assert.All(rows, row => { Assert.Equal(category, row.Category); Assert.Equal(trace, row.TraceId); });
            var before = rows[0].Id;
            var page = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category={category}&traceId={trace}&before={before}"))!;
            Assert.All(page, row => Assert.True(row.Id < before));
        }
        var access = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category=access&traceId={trace}&result=success"))!;
        Assert.Contains(access, row => row.Action == "billing.report.export");
        var conversation = await ChatApi.CreateConversation(member);
        var operations = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category=activity&result=success&search={conversation.Id}"))!;
        var created = Assert.Single(operations);
        Assert.Equal("conversation.created", created.Action); Assert.Equal("completed", created.Result);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit?category=unsupported")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit?traceId=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/v1/admin/audit?category=authentication&traceId={trace}")).StatusCode);
    }

    [Fact]
    public async Task AuditFailureFilterExcludesAcceptedLifecycleAndLegacyOperations()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var user = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var result in new[] { "created", "deleted", "soft_deleted", "queued", "read-only", "cancelled", "executor_lost" })
                db.AuditEvents.Add(new() { OwnerId = user.Id, Action = "fixture.lifecycle", Result = result });
            await db.SaveChangesAsync();
        }
        var failures = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?result=failed"))!;
        Assert.Equal("executor_lost", Assert.Single(failures).Result);
        var accepted = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.lifecycle&result=success"))!;
        Assert.Equal(6, accepted.Length);
        // 多個結果條件符合任一即可；群組與精確結果可以混用，同樣是符合任一。
        var chosen = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.lifecycle&result=created&result=deleted"))!;
        Assert.Equal(["deleted", "created"], chosen.Select(x => x.Result));
        var mixed = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.lifecycle&result=failed&result=queued"))!;
        Assert.Equal(["executor_lost", "queued"], mixed.Select(x => x.Result));
        Assert.Equal(7, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.lifecycle&result=failed&result=success"))!.Length);
    }

    [Fact]
    public async Task PageSizeIsOneOfTheOfferedSizes()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var user = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.AddRange(Enumerable.Range(0, 60).Select(_ => new AuditEvent { OwnerId = user.Id, Action = "fixture.page", Result = "saved" }));
            await db.SaveChangesAsync();
        }
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
    public async Task CatalogOffersServerClassifiedActionsAndCategories()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var catalog = (await admin.GetFromJsonAsync<AuditCatalogDto>("/api/v1/admin/audit/catalog"))!;
        Assert.Equal(AuditCategories.Values, catalog.Categories.Select(x => x.Value));
        Assert.Equal(catalog.Actions.Count, catalog.Actions.Select(x => x.Value).Distinct().Count());
        Assert.All(catalog.Actions, action => { Assert.NotEmpty(action.Label); Assert.NotEmpty(action.Categories); Assert.Subset(AuditCategories.Values.ToHashSet(), action.Categories.ToHashSet()); });
        Assert.Equal(["authentication"], Assert.Single(catalog.Actions, x => x.Value == "identity.login").Categories);
        Assert.Equal(["access"], Assert.Single(catalog.Actions, x => x.Value == "logs.").Categories);
        Assert.Equal(["access"], Assert.Single(catalog.Actions, x => x.Value == "audit.export").Categories);
        // 前綴同時涵蓋自身分類，以及其下的管理讀取動作。
        Assert.Equal(["administration", "access"], Assert.Single(catalog.Actions, x => x.Value == "admin.").Categories);
        Assert.Equal(["activity", "access"], Assert.Single(catalog.Actions, x => x.Value == "integration.").Categories);
    }

    [Fact]
    public async Task ExportHoldsTheWholeQueryIsAuditedAndRefusesMoreThanTheLimit()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: services => services.Configure<AuditOptions>(x => x.MaxExportRows = 3));
        using var admin = await factory.SignedInAsync(); using var member = await factory.SignedInAsync("bob");
        var user = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.AddRange(
                new AuditEvent { OwnerId = user.Id, Action = "fixture.export", Result = "saved", DetailsJson = """{"resourceKey":"=HYPERLINK(\"evil\")"}""" },
                new AuditEvent { OwnerId = user.Id, Action = "fixture.export", Result = "failed" },
                new AuditEvent { OwnerId = user.Id, Action = "fixture.overflow", Result = "saved" },
                new AuditEvent { OwnerId = user.Id, Action = "fixture.overflow", Result = "saved" },
                new AuditEvent { OwnerId = user.Id, Action = "fixture.overflow", Result = "saved" },
                new AuditEvent { OwnerId = user.Id, Action = "fixture.overflow", Result = "saved" });
            await db.SaveChangesAsync();
        }
        using var response = await admin.GetAsync("/api/v1/admin/audit/export?action=fixture.export&result=saved&result=failed");
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lines = System.Text.Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("\"紀錄 ID\",\"時間（UTC）\",\"操作者\"", lines[0]);
        Assert.Equal(3, lines.Length);
        // 依時間由新到舊排序；每個儲存格中類似公式的文字都會被中和。
        Assert.Contains("\"failed\"", lines[1]);
        Assert.Contains("\"'=HYPERLINK(\"\"evil\"\")\"", lines[2]);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var record = Assert.Single(db.AuditEvents.AsNoTracking().Where(x => x.Action == "audit.export"));
            Assert.Equal(user.Id, record.OwnerId); Assert.Equal("read", record.Result);
            Assert.Contains("\"action\":\"fixture.export\"", record.DetailsJson); Assert.Contains("\"results\":[\"saved\",\"failed\"]", record.DetailsJson);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit/export?action=fixture.overflow")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            // 超過上限被拒的嘗試同樣留有紀錄。
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Single(db.AuditEvents.AsNoTracking().Where(x => x.Action == "audit.export" && x.DetailsJson!.Contains("fixture.overflow")));
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/admin/audit/export?action=fixture.export")).StatusCode);
    }

    [Fact]
    public async Task ExportCombinesResultGroupsWithAnActionPrefix()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var user = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.AddRange(
                new AuditEvent { OwnerId = user.Id, Action = "fixture.multi.saved", Result = "saved" },
                new AuditEvent { OwnerId = user.Id, Action = "fixture.multi.failed", Result = "failed" },
                new AuditEvent { OwnerId = user.Id, Action = "fixture.multi.read", Result = "read" },
                new AuditEvent { OwnerId = user.Id, Action = "fixture.other", Result = "failed" });
            await db.SaveChangesAsync();
        }
        async Task<string[]> ActionsAsync(string query)
        {
            var text = (await admin.GetStringAsync("/api/v1/admin/audit/export?" + query)).TrimStart('\uFEFF');
            return text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(line => line.Split("\",\"")[4]).Order().ToArray();
        }
        // 群組與精確結果混用時符合任一即可；動作前綴排除不相干的 fixture.other。
        Assert.Equal(["fixture.multi.failed", "fixture.multi.read"], await ActionsAsync("action=fixture.multi.&result=failed&result=read"));
        Assert.Equal(["fixture.multi.read", "fixture.multi.saved"], await ActionsAsync("action=fixture.multi.&result=success"));
    }

    [Fact]
    public async Task ExportIsRateLimitedPerUser()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++) statuses.Add((await admin.GetAsync("/api/v1/admin/audit/export?action=fixture.none")).StatusCode);
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }
}
