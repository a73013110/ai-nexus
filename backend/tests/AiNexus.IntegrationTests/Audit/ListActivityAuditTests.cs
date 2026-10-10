using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Administration.Users;
using AiNexus.Features.Audit;
using AiNexus.Features.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Audit;

public sealed class ListActivityAuditTests
{
    [Fact]
    public async Task AuditorsCanInvestigateWithoutManagingAccountsAndRevocationAppliesImmediately()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
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
        Assert.DoesNotContain(me.Access.Features, feature => feature.Id == "admin");
        var catalog = (await auditor.GetFromJsonAsync<AuditCatalogDto>("/api/v1/admin/audit/catalog"))!;
        Assert.Contains(catalog.Features, feature => feature.Id == "audit");
        Assert.NotEmpty(catalog.Models);
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
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); using var member = await factory.SignedInAsync("bob");
        var owner = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        const string trace = "1234567890abcdef1234567890abcdef";
        string[] actions = ["identity.login", "admin.user_roles", "resource.acl.updated", "system.diagnostics.configuration", "billing.price.created",
            "logs.query", "admin.conversation_read", "billing.report.export", "dashboard.platform.read", "integration.record.read", "conversation.created"];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
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
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var user = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            foreach (var result in new[] { "created", "deleted", "soft_deleted", "queued", "read-only", "cancelled", "executor_lost" })
                db.AuditEvents.Add(new() { OwnerId = user.Id, Action = "fixture.lifecycle", Result = result });
            await db.SaveChangesAsync();
        }
        var failures = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?result=failed"))!;
        Assert.Equal("executor_lost", Assert.Single(failures).Result);
        var accepted = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=fixture.lifecycle&result=success"))!;
        Assert.Equal(6, accepted.Length);
    }
}
