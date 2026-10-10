using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Account;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Chat;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ChatApi;
using static AiNexus.IntegrationTests.Support.ProjectApi;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class FeatureAccessTests
{
    [Fact]
    public async Task FirstLoginCreatesMemberAndResolvesGroupFeatures()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal("member", Assert.Single(me.Access.Roles).Id);
        Assert.Equal("workspace", Assert.Single(me.Access.Groups).Id);
        Assert.Contains(new FeatureDto("chat", "對話", "/chat"), me.Access.Features);
        Assert.Equal(me.Access.Features.Count, me.Access.Features.Select(x => x.Id).Distinct().Count());
        (await client.GetAsync("/api/v1/models")).EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("role")]
    [InlineData("group")]
    [InlineData("feature")]
    [InlineData("membership")]
    public async Task RevocationAppliesOnNextRequestAndLoginDoesNotRegrant(string target)
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            switch (target)
            {
                case "role": (await db.Set<Role>().SingleAsync(x => x.Id == "member")).Enabled = false; break;
                case "group": (await db.Set<RoleGroup>().SingleAsync(x => x.Id == "workspace")).Enabled = false; break;
                case "feature": (await db.Set<Feature>().SingleAsync(x => x.Id == "chat")).Enabled = false; break;
                case "membership": db.Set<UserRole>().RemoveRange(db.Set<UserRole>()); break;
            }
            await db.SaveChangesAsync();
        }
        // /me remains usable for account/preferences even when chat is unavailable.
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
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
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
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        Assert.Contains((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == "files" && x.Name == "檔案庫");
        (await admin.GetAsync("/api/v1/files")).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/files", new FeatureUpdateRequest("檔案庫", 15, false))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/files")).StatusCode);
        Assert.DoesNotContain((await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "files");
    }

    // Only the effective-feature read of AccessService joins this table.
    private const string GrantRead = "RoleGroupFeatures";

    [Fact]
    public async Task GrantsAreReadOncePerRequestAndRevocationAppliesOnTheNextRequest()
    {
        await using var factory = new NexusFactory(services: RequestQueries.Register);
        using var client = await factory.SignedInAsync();
        var project = await CreateProject(client, "授權快取");
        await AddProjectFile(client, project.Resource.Id, "a.txt");
        var queries = factory.Services.GetRequiredService<RequestQueries>();
        var path = $"/api/v1/projects/{project.Resource.Id}/files";
        queries.Clear();
        (await client.GetAsync(path)).EnsureSuccessStatusCode();
        // The feature policy, the project ACL and the document check each need the grants.
        Assert.Equal(1, queries.Count(path, GrantRead));
        (await client.GetAsync(path)).EnsureSuccessStatusCode();
        Assert.Equal(2, queries.Count(path, GrantRead));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<RoleGroupFeature>().Where(x => x.FeatureId == FeatureIds.Projects).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task AdministrativeChangeInTheSameRequestReadsGrantsAgain()
    {
        await using var factory = new NexusFactory(administrators: ["alice"], services: RequestQueries.Register);
        using var alice = await factory.SignedInAsync();
        var me = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        // The request authorized with administrator grants; removing them must still be detected after the write.
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PutAsJsonAsync($"/api/v1/admin/users/{me.Id}/roles", new AiNexus.Features.Administration.Users.UserRolesRequest(["member"]))).StatusCode);
        Assert.Contains((await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "admin");
    }
}
