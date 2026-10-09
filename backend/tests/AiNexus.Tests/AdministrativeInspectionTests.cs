using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static AiNexus.Tests.ChatApiTests;
using AiNexus.Features.Audit;

namespace AiNexus.Tests;

public sealed class AdministrativeInspectionTests
{
    [Fact]
    public async Task AdministrativeReadIsExplicitAuditedAndDoesNotWeakenOwnerEndpoints()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var user = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        var conversation = await CreateConversation(bob);
        var run = await CreateRun(bob, conversation.Id, "不可寫入稽核的私人提問"); await WaitForTerminal(bob, run.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync($"/api/v1/admin/users/{user.Id}/insights")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync($"/api/v1/admin/users/{user.Id}/conversations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync($"/api/v1/admin/conversations/{conversation.Id}")).StatusCode);
        var detail = (await admin.GetFromJsonAsync<AdminConversationDetailDto>($"/api/v1/admin/conversations/{conversation.Id}"))!;
        Assert.Equal(user.Account, detail.OwnerAccount); Assert.Equal(2, detail.Total);
        Assert.Contains(detail.Messages, x => x.Content == "不可寫入稽核的私人提問");
        var insights = (await admin.GetFromJsonAsync<AdminUserDetailDto>($"/api/v1/admin/users/{user.Id}/insights"))!;
        Assert.Equal(1, insights.Usage.Requests); Assert.Equal(123, insights.Usage.InputTokens);
        var audit = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.conversation_read&result=read"))!;
        var read = Assert.Single(audit); Assert.Equal(conversation.Id, read.ResourceId);
        Assert.DoesNotContain("不可寫入稽核", read.DetailsJson);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);
        (await bob.GetAsync($"/api/v1/conversations/{conversation.Id}")).EnsureSuccessStatusCode();
    }
    [Fact]
    public async Task AllVersionsAndDeletedConversationsArePaginatedAndSearchRemainsMetadataOnlyInAudit()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var user = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        var conversation = await CreateConversation(bob);
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var value = await db.Conversations.FindAsync(conversation.Id); value!.IsDeleted = true; value.IsArchived = true;
            for (var i = 0; i < 51; i++) db.Conversations.Add(new() { OwnerId = user.Id, Title = "其他對話" + i });
            for (var i = 0; i < 101; i++) db.Messages.Add(new() { ConversationId = conversation.Id, Content = "敏感搜尋字" + i, CreatedAt = DateTimeOffset.UtcNow.AddSeconds(i) });
            await db.SaveChangesAsync();
        }
        var first = (await admin.GetFromJsonAsync<AdminConversationPageDto>($"/api/v1/admin/users/{user.Id}/conversations?includeDeleted=true"))!;
        Assert.Equal(52, first.Total); Assert.Equal(50, first.Items.Count);
        Assert.Equal(2, (await admin.GetFromJsonAsync<AdminConversationPageDto>($"/api/v1/admin/users/{user.Id}/conversations?includeDeleted=true&offset=50"))!.Items.Count);
        Assert.Equal(51, (await admin.GetFromJsonAsync<AdminConversationPageDto>($"/api/v1/admin/users/{user.Id}/conversations"))!.Total);
        var found = (await admin.GetFromJsonAsync<AdminConversationPageDto>($"/api/v1/admin/users/{user.Id}/conversations?includeDeleted=true&search={Uri.EscapeDataString("敏感搜尋字")}"))!;
        Assert.Equal(conversation.Id, Assert.Single(found.Items).Id);
        Assert.Equal(100, (await admin.GetFromJsonAsync<AdminConversationDetailDto>($"/api/v1/admin/conversations/{conversation.Id}"))!.Messages.Count);
        Assert.Single((await admin.GetFromJsonAsync<AdminConversationDetailDto>($"/api/v1/admin/conversations/{conversation.Id}?offset=100"))!.Messages);
        using var check = factory.Services.CreateScope();
        Assert.All(await check.ServiceProvider.GetRequiredService<NexusDbContext>().AuditEvents.Where(x => x.Action.StartsWith("admin.conversation")).ToListAsync(), x => Assert.DoesNotContain("敏感搜尋字", x.DetailsJson));
    }
    [Fact]
    public async Task ReportsIncludeOtherAiTasksAndIgnoreOtherOwnersAndOldUsage()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var actor = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!; var user = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Set<ModelInvocation>().AddRange(
                new() { OwnerId = user.Id, Kind = "evaluation", ModelId = "test-model", Status = "completed", InputTokens = 600, OutputTokens = 200 },
                new() { OwnerId = user.Id, Kind = "ocr", ModelId = "test-model", Status = "failed" },
                new() { OwnerId = user.Id, Kind = "transform", ModelId = "test-model", Status = "completed", InputTokens = 900, OutputTokens = 500, CreatedAt = DateTimeOffset.UtcNow.AddDays(-40) },
                new() { OwnerId = actor.Id, Kind = "transform", ModelId = "test-model", Status = "completed", InputTokens = 1234, OutputTokens = 123 });
            await db.SaveChangesAsync();
        }
        var detail = (await admin.GetFromJsonAsync<AdminUserDetailDto>($"/api/v1/admin/users/{user.Id}/insights"))!;
        Assert.Equal(2, detail.Usage.Requests); Assert.Equal(600, detail.Usage.InputTokens); Assert.Equal(1, detail.Usage.RequestsWithUsage);
        Assert.Equal(2, detail.Kinds.Count); Assert.Equal(2, detail.Usage.Daily.Sum(x => x.Requests));
        var users = (await admin.GetFromJsonAsync<AdminUsersDto>("/api/v1/admin/users"))!;
        Assert.Equal(600, users.Users.Single(x => x.Id == user.Id).Activity!.Usage.InputTokens);
    }
    [Fact]
    public async Task FailedMutationsAreRolledBackAuditedAndFiltersKeepAStableCursor()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PutAsJsonAsync("/api/v1/admin/roles/analyst", new RoleUpdateRequest("分析人員", true, ["workspace"]))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/roles/analyst", new RoleUpdateRequest("資深分析人員", true, ["workspace"]))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/v1/admin/roles/analyst", new RoleUpdateRequest("不可儲存的名稱", true, ["missing"]))).StatusCode);
        var failed = Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.role&result=failed"))!);
        Assert.Equal("unknown_access_id", failed.Result);
        var saved = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?action=admin.role&result=saved&before={failed.Id}&search=analyst"))!;
        Assert.Equal(2, saved.Length); Assert.True(saved[0].Id > saved[1].Id);
        using var json = JsonDocument.Parse(saved[0].DetailsJson!);
        Assert.Equal("分析人員", json.RootElement.GetProperty("before").GetProperty("name").GetString());
        Assert.Equal("資深分析人員", json.RootElement.GetProperty("after").GetProperty("name").GetString());
        Assert.Equal("資深分析人員", (await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Roles.Single(x => x.Id == "analyst").Name);
        var cursor = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?action=admin.role&before={saved[0].Id}&result=saved"))!;
        Assert.Equal(saved[1].Id, Assert.Single(cursor).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit?from=2026-10-05&until=2026-10-01")).StatusCode);
    }
    [Fact]
    public async Task RevokedAdministratorCannotReadEvenWithAnExistingSession()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var me = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Remove(await db.Set<UserRole>().SingleAsync(x => x.UserId == me.Id && x.RoleId == "administrator")); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/v1/admin/users/{me.Id}/insights")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/v1/admin/users/{me.Id}/conversations")).StatusCode);
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
