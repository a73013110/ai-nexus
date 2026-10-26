using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AiNexus.Features.Work.Chat.Context;
using static AiNexus.IntegrationTests.Support.ChatApi;
using static AiNexus.IntegrationTests.Support.ProjectApi;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Operations;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class FeatureAccessTests
{
    [Fact]
    public async Task FirstLoginCreatesMemberAndResolvesGroupFeatures()
    {
        await using var factory = new AppFactory();
        using var client = await factory.SignedInAsync();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal("member", Assert.Single(me.Access.Roles).Id);
        Assert.Equal("workspace", Assert.Single(me.Access.Groups).Id);
        Assert.Contains(new FeatureDto("chat", "對話", "/work/chat", "chat"), me.Access.Features);
        Assert.Equal(me.Access.Features.Count, me.Access.Features.Select(x => x.Id).Distinct().Count());
        // 只列宣告了範圍的功能，畫面據此決定版面（只看得到本人時不顯示選人）。
        Assert.Equal([DataScopes.Own], me.Access.DataScopes[FeatureCatalog.AttendanceRecords]);
        Assert.DoesNotContain(FeatureCatalog.Chat, me.Access.DataScopes.Keys);
        (await client.GetAsync("/api/v1/models")).EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("role")]
    [InlineData("group")]
    [InlineData("feature")]
    [InlineData("membership")]
    public async Task RevocationAppliesOnNextRequestAndLoginDoesNotRegrant(string target)
    {
        await using var factory = new AppFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            switch (target)
            {
                case "role": (await db.Set<Role>().SingleAsync(x => x.Id == "member")).Enabled = false; break;
                case "group": (await db.Set<RoleGroup>().SingleAsync(x => x.Id == "workspace")).Enabled = false; break;
                case "feature": (await db.Set<Feature>().SingleAsync(x => x.Id == "chat")).Enabled = false; break;
                case "membership": db.Set<UserRole>().RemoveRange(db.Set<UserRole>()); break;
            }
            await db.SaveChangesAsync();
        }
        // 即使聊天功能不可用，/me 仍可用於帳號與偏好設定。
        Assert.DoesNotContain((await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "chat");
        foreach (var route in new[] { "models", "conversations", $"conversations/{conversation.Id}" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/{route}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(null, null, "hello", "test-model"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostRun(client, new(conversation.Id, "test-model", "hello", null, null))).StatusCode);
        using var newSession = await factory.SignedInAsync();
        Assert.DoesNotContain((await newSession.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "chat");
    }

    [Fact]
    public async Task MultipleRolesAndGroupsProduceUniqueFeatures()
    {
        await using var factory = new AppFactory();
        using var client = await factory.SignedInAsync();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<Role>().Add(new() { Id = "reviewer", Name = "覆核人員" });
            db.Set<RoleGroup>().Add(new() { Id = "review", Name = "覆核工作區" });
            db.Set<UserRole>().Add(new() { UserId = me.Id, RoleId = "reviewer" });
            db.Set<RoleGroupRole>().AddRange(new RoleGroupRole { RoleId = "reviewer", GroupId = "workspace" }, new RoleGroupRole { RoleId = "reviewer", GroupId = "review" });
            db.Set<RoleGroupFeature>().Add(new() { GroupId = "review", FeatureId = "chat" });
            await db.SaveChangesAsync();
        }
        var access = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access;
        Assert.Equal(2, access.Roles.Count);
        Assert.Equal(2, access.Groups.Count);
        Assert.Equal(access.Features.Count, access.Features.Select(x => x.Id).Distinct().Count());
        Assert.Single(access.Features, x => x.Id == "chat");
    }

    [Fact]
    public async Task FileLibraryIsRegisteredAndRevocationBlocksItsApi()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        Assert.Contains((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == "files" && x.Name == "檔案庫");
        (await admin.GetAsync("/api/v1/files")).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/files", new FeatureUpdateRequest("檔案庫", false))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/files")).StatusCode);
        Assert.DoesNotContain((await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "files");
    }

    [Fact]
    public async Task OperationsAndAccessManagementAreSeparateGrants()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var features = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features;
        Assert.Contains(new FeatureDto("identity.users", "使用者", "/admin/identity/users", "user"), features);
        Assert.Contains(new FeatureDto("operations.usage", "平台用量", "/admin/operations/usage", "chart"), features);
        await PlatformGrants.SplitOperationsAsync(factory, (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id);

        var operations = new[] { "admin/usage", "admin/billing/prices", "admin/billing/targets", "admin/billing/spend", "admin/knowledge/profiles",
            "admin/knowledge/capabilities", "dashboard?scope=platform" };
        var accessManagement = new[] { "admin/catalog", "admin/users" };
        foreach (var route in operations)
        {
            (await bob.GetAsync($"/api/v1/{route}")).EnsureSuccessStatusCode();
            await AssertFeatureForbiddenAsync(await alice.GetAsync($"/api/v1/{route}"));
        }
        foreach (var route in accessManagement)
        {
            (await alice.GetAsync($"/api/v1/{route}")).EnsureSuccessStatusCode();
            await AssertFeatureForbiddenAsync(await bob.GetAsync($"/api/v1/{route}"));
        }
        await AssertFeatureForbiddenAsync(await alice.PostAsync("/api/v1/admin/knowledge/capabilities/probe", null));
        await AssertFeatureForbiddenAsync(await bob.PutAsJsonAsync("/api/v1/admin/features/files", new FeatureUpdateRequest("檔案庫", false)));
        var bobFeatures = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features;
        Assert.Contains(bobFeatures, x => x.Id == FeatureCatalog.OperationsUsage);
        Assert.DoesNotContain(bobFeatures, x => x.Id == FeatureCatalog.IdentityUsers);
    }

    /// <summary>無論缺少的授權來自 policy 或 handler 自行檢查，都回應同一個共用代碼。</summary>
    private static async Task AssertFeatureForbiddenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("feature_forbidden", (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("code").GetString());
    }

    // 只有 AccessService 的有效功能查詢會 join 這張表。
    private const string GrantRead = "RoleGroupFeatures";

    [Fact]
    public async Task GrantsAreReadOncePerRequestAndRevocationAppliesOnTheNextRequest()
    {
        await using var factory = new AppFactory(services: RequestQueries.Register);
        using var client = await factory.SignedInAsync();
        var project = await CreateProject(client, "授權快取");
        await AddProjectFile(client, project.Resource.Id, "a.txt");
        var queries = factory.Services.GetRequiredService<RequestQueries>();
        var path = $"/api/v1/projects/{project.Resource.Id}/files";
        queries.Clear();
        (await client.GetAsync(path)).EnsureSuccessStatusCode();
        // 功能 policy、專案 ACL 與文件檢查各自都需要授權。
        Assert.Equal(1, queries.Count(path, GrantRead));
        (await client.GetAsync(path)).EnsureSuccessStatusCode();
        Assert.Equal(2, queries.Count(path, GrantRead));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<RoleGroupFeature>().Where(x => x.FeatureId == FeatureCatalog.Projects).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task AdministrativeChangeInTheSameRequestReadsGrantsAgain()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: RequestQueries.Register);
        using var alice = await factory.SignedInAsync();
        var me = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        // 請求原本以管理員授權通過；寫入後撤銷這些授權，仍必須被偵測到。
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PutAsJsonAsync($"/api/v1/admin/users/{me.Id}/roles", new UserRolesRequest(["member"]))).StatusCode);
        Assert.Contains((await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == FeatureCatalog.IdentityUsers);
    }
}
