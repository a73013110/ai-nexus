using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Account;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects;
using AiNexus.Features.Quality;
using AiNexus.Features.Sharing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;

/// <summary>SQL commands issued while serving a request path; background work has no request and is not counted.</summary>
public sealed class RequestQueries(IHttpContextAccessor http) : DbCommandInterceptor
{
    private readonly ConcurrentQueue<(string Path, string Sql)> commands = new();

    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<RequestQueries>();
        services.ConfigureDbContext<NexusDbContext>((provider, options) => options.AddInterceptors(provider.GetRequiredService<RequestQueries>()));
    }

    public void Clear() => commands.Clear();
    /// <summary>Commands for <paramref name="path"/> whose SQL contains every one of <paramref name="texts"/>.</summary>
    public int Count(string path, params string[] texts) => commands.Count(x => x.Path == path && texts.All(text => x.Sql.Contains(text, StringComparison.Ordinal)));

    private void Record(DbCommand command)
    {
        if (http.HttpContext?.Request.Path.Value is { } path) commands.Enqueue((path, command.CommandText));
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result) { Record(command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default) { Record(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData data, InterceptionResult<object> result) { Record(command); return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<object> result, CancellationToken ct = default) { Record(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data, InterceptionResult<int> result) { Record(command); return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default) { Record(command); return ValueTask.FromResult(result); }
}

public sealed class QueryEfficiencyTests
{
    // Only the effective-feature read of AccessService joins this table.
    private const string GrantRead = "RoleGroupFeatures";

    private static async Task<ProjectDto> CreateProject(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/projects", new ProjectRequest(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    private static async Task<DocumentDto> AddProjectFile(HttpClient client, Guid project, string name)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("專案參考：" + name)), "file", name);
        using var uploaded = await client.PostAsync("/api/v1/attachments", body);
        uploaded.EnsureSuccessStatusCode();
        var file = (await uploaded.Content.ReadFromJsonAsync<AttachmentDto>())!;
        using var added = await client.PostAsJsonAsync($"/api/v1/projects/{project}/files", new AddDocumentRequest(file.Id));
        added.EnsureSuccessStatusCode();
        return (await added.Content.ReadFromJsonAsync<DocumentDto>())!;
    }

    private static async Task Share(HttpClient owner, string route, params (Guid User, string Role)[] members)
        => (await owner.PutAsJsonAsync(route, new ResourceAclRequest(members.Select(x => new ResourceMemberUpdate(x.User, x.Role)).ToArray(), []))).EnsureSuccessStatusCode();

    [Fact]
    public async Task GrantsAreReadOncePerRequestAndRevocationAppliesOnTheNextRequest()
    {
        await using var factory = new NexusFactory(backgroundJobs: false, services: RequestQueries.Register);
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
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PutAsJsonAsync($"/api/v1/admin/users/{me.Id}/roles", new AiNexus.Features.Administration.UserRolesRequest(["member"]))).StatusCode);
        Assert.Contains((await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "admin");
    }

    [Fact]
    public async Task ProjectFilesMatchEachDocumentDetailForOwnerViewerAndEditorInFixedQueries()
    {
        await using var factory = new NexusFactory(backgroundJobs: false, services: RequestQueries.Register);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var project = await CreateProject(alice, "參考檔案");
        var id = project.Resource.Id; var path = $"/api/v1/projects/{id}/files";
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(path)).StatusCode);
        await AddProjectFile(alice, id, "first.txt");
        var queries = factory.Services.GetRequiredService<RequestQueries>();
        queries.Clear(); (await alice.GetAsync(path)).EnsureSuccessStatusCode(); var single = queries.Count(path);
        await AddProjectFile(alice, id, "second.txt"); await AddProjectFile(alice, id, "third.txt");
        queries.Clear(); (await alice.GetAsync(path)).EnsureSuccessStatusCode();
        Assert.Equal(single, queries.Count(path));

        await AssertMatchesDetail(alice, path, canEdit: true);
        await Share(alice, $"/api/v1/projects/{id}/access", (bobId, "viewer"));
        await AssertMatchesDetail(bob, path, canEdit: false);
        await Share(alice, $"/api/v1/projects/{id}/access", (bobId, "editor"));
        await AssertMatchesDetail(bob, path, canEdit: true);

        static async Task AssertMatchesDetail(HttpClient client, string path, bool canEdit)
        {
            var list = (await client.GetFromJsonAsync<DocumentDto[]>(path))!;
            Assert.Equal(3, list.Length);
            foreach (var item in list)
            {
                Assert.Equal(canEdit, item.CanEdit);
                Assert.Equal(await client.GetFromJsonAsync<DocumentDto>($"/api/v1/documents/{item.Id}"), item);
            }
        }
    }

    [Fact]
    public async Task ProjectAndEvaluationSetListsMatchEachItemAndMarkOnlyEditableItems()
    {
        await using var factory = new NexusFactory(backgroundJobs: false, services: RequestQueries.Register);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        using var carol = await factory.SignedInAsync("carol");
        var aliceId = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var own = await CreateProject(alice, "自己的");
        var viewed = await CreateProject(bob, "只能看");
        var edited = await CreateProject(carol, "可編輯");
        await CreateProject(carol, "看不到");
        var queries = factory.Services.GetRequiredService<RequestQueries>();
        await Share(bob, $"/api/v1/projects/{viewed.Resource.Id}/access", (aliceId, "viewer"));
        queries.Clear(); (await alice.GetAsync("/api/v1/projects")).EnsureSuccessStatusCode(); var fewer = queries.Count("/api/v1/projects");
        await Share(carol, $"/api/v1/projects/{edited.Resource.Id}/access", (aliceId, "editor"));
        // The list costs the same however many shared projects it describes.
        queries.Clear(); (await alice.GetAsync("/api/v1/projects")).EnsureSuccessStatusCode();
        Assert.Equal(fewer, queries.Count("/api/v1/projects"));
        var projects = (await alice.GetFromJsonAsync<ProjectDto[]>("/api/v1/projects"))!;
        Assert.Equal(new[] { edited.Resource.Id, viewed.Resource.Id, own.Resource.Id }.Order(), projects.Select(x => x.Resource.Id).Order());
        Assert.Equal([true, false, true], new[] { own, viewed, edited }.Select(p => projects.Single(x => x.Resource.Id == p.Resource.Id).Resource.CanEdit));
        foreach (var item in projects) Assert.Equal(await alice.GetFromJsonAsync<ProjectDto>($"/api/v1/projects/{item.Resource.Id}"), item);

        async Task<EvaluationSetDto> CreateSet(HttpClient client, string name)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/quality/sets", new EvaluationSetRequest(name, "說明", [new EvaluationCase("採購需要誰核准？")]));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<EvaluationSetDto>())!;
        }
        var mine = await CreateSet(alice, "我的題庫");
        var readOnly = await CreateSet(bob, "唯讀題庫");
        var editable = await CreateSet(carol, "共編題庫");
        await CreateSet(carol, "私人題庫");
        await Share(bob, $"/api/v1/quality/sets/{readOnly.Resource.Id}/access", (aliceId, "viewer"));
        await Share(carol, $"/api/v1/quality/sets/{editable.Resource.Id}/access", (aliceId, "editor"));
        var sets = (await alice.GetFromJsonAsync<EvaluationSetDto[]>("/api/v1/quality/sets"))!;
        Assert.Equal(3, sets.Length);
        Assert.Equal([true, false, true], new[] { mine, readOnly, editable }.Select(s => sets.Single(x => x.Resource.Id == s.Resource.Id).Resource.CanEdit));
        foreach (var item in sets)
        {
            var detail = (await alice.GetFromJsonAsync<EvaluationSetDto>($"/api/v1/quality/sets/{item.Resource.Id}"))!;
            Assert.Equal(detail.Resource, item.Resource); Assert.Equal(detail.Version, item.Version); Assert.Equal(detail.Cases.Single(), item.Cases.Single());
        }
    }

    [Fact]
    public async Task ShareListsKeepSenderRecipientAndMissingSourceRules()
    {
        await using var factory = new NexusFactory(backgroundJobs: false);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        using var carol = await factory.SignedInAsync("carol");
        var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var carolId = (await carol.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var conversation = await CreateConversation(alice);
        using var created = await alice.PostAsJsonAsync("/api/v1/artifacts", new CreateArtifactRequest("成果", "內容"));
        created.EnsureSuccessStatusCode();
        var artifact = (await created.Content.ReadFromJsonAsync<ArtifactDto>())!;
        async Task<ShareDto> CreateShare(CreateShareRequest request)
        {
            using var response = await alice.PostAsJsonAsync("/api/v1/shares", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ShareDto>())!;
        }
        var chat = await CreateShare(new("conversation", conversation.Id, [bobId, carolId]));
        var document = await CreateShare(new("artifact", artifact.Resource.Id, [bobId]));
        (await carol.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", (await CreateConversation(carol)).Id, [bobId]))).EnsureSuccessStatusCode();

        var sent = (await alice.GetFromJsonAsync<ShareDto[]>("/api/v1/shares?sent=true"))!;
        Assert.Equal([document.Id, chat.Id], sent.Select(x => x.Id));
        Assert.All(sent, x => { Assert.True(x.IsOwner); Assert.Equal("alice", x.Owner); });
        Assert.Equal(["bob", "carol"], sent.Single(x => x.Id == chat.Id).Recipients.Order());
        Assert.Equal(["bob"], sent.Single(x => x.Id == document.Id).Recipients);
        var received = (await bob.GetFromJsonAsync<ShareDto[]>("/api/v1/shares"))!;
        Assert.Equal(3, received.Length);
        Assert.All(received, x => { Assert.False(x.IsOwner); Assert.Empty(x.Recipients); });
        Assert.Equal("carol", received[0].Owner); Assert.Equal(["alice", "alice"], received.Skip(1).Select(x => x.Owner));
        Assert.Empty((await alice.GetFromJsonAsync<ShareDto[]>("/api/v1/shares"))!);

        // A source that disappeared without revoking its share is hidden from recipients but still listed for the sender.
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<WorkspaceResource>().Where(x => x.Id == artifact.Resource.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true));
        Assert.DoesNotContain((await bob.GetFromJsonAsync<ShareDto[]>("/api/v1/shares"))!, x => x.Id == document.Id);
        Assert.Contains((await alice.GetFromJsonAsync<ShareDto[]>("/api/v1/shares?sent=true"))!, x => x.Id == document.Id);
        Assert.Single((await carol.GetFromJsonAsync<ShareDto[]>("/api/v1/shares"))!);
    }

    [Fact]
    public async Task SearchReadsCollectionAccessOnceBeforeRemoteWorkAndOnceBeforeReturning()
    {
        await using var factory = new NexusFactory(backgroundJobs: false, services: RequestQueries.Register);
        using var client = await factory.SignedInAsync(); var seed = await RetrievalPipelineTests.SeedAsync(factory, client);
        var queries = factory.Services.GetRequiredService<RequestQueries>(); queries.Clear();
        using var response = await client.PostAsJsonAsync("/api/v1/knowledge/search", new KnowledgeSearchRequest("採購", [seed.Collection]));
        response.EnsureSuccessStatusCode();
        Assert.Single((await response.Content.ReadFromJsonAsync<KnowledgeSearchDto>())!.Hits);
        // Collection ACL: once at the start and once, with fresh grants, before the result leaves.
        Assert.Equal(2, queries.Count("/api/v1/knowledge/search", "\"ResourceGroups\""));
        Assert.Equal(2, queries.Count("/api/v1/knowledge/search", GrantRead));
    }

    [Fact]
    public async Task ExistingWindowsUsersAreResolvedWithoutTheIdentityWriteGate()
    {
        await using var factory = new NexusFactory(backgroundJobs: false);
        using var client = await factory.SignedInAsync();
        var gate = factory.Services.GetRequiredService<IdentityWriteLock>().Gate;
        await gate.WaitAsync();
        try { (await client.GetAsync("/api/v1/me").WaitAsync(TimeSpan.FromSeconds(10))).EnsureSuccessStatusCode(); }
        finally { gate.Release(); }

        // A user due a last-seen refresh still waits for the gate, then is updated.
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, DateTimeOffset.UtcNow.AddHours(-1)));
        await gate.WaitAsync();
        Task<HttpResponseMessage> pending;
        try
        {
            pending = client.GetAsync("/api/v1/me");
            await Task.Delay(200);
            Assert.False(pending.IsCompleted);
        }
        finally { gate.Release(); }
        (await pending).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.SingleAsync(x => x.Account == "TEST\\alice")).LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task CookieRequestsReadTheSignedInUserOnce()
    {
        await using var factory = new NexusFactory(ldap: true, services: RequestQueries.Register);
        using var client = factory.CreateClient();
        var session = (await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;
        client.DefaultRequestHeaders.Add("X-Nexus-CSRF", session.CsrfToken);
        (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("alice", "fixture-password"))).EnsureSuccessStatusCode();
        var queries = factory.Services.GetRequiredService<RequestQueries>(); queries.Clear();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal("alice", me.Account.Split('@')[0]);
        // Cookie validation reads the row; the current user reuses it instead of a second single-row read.
        Assert.Equal(1, queries.Count("/api/v1/me", "FROM \"Users\" AS", "LIMIT 2"));
    }

    [Fact]
    public void DisplayNamesAreCachedPerInstance()
    {
        var calls = 0;
        var first = new DisplayNameCache(TimeProvider.System);
        Assert.Equal("甲", first.GetOrAdd("S-1", () => { calls++; return "甲"; }));
        Assert.Equal("甲", first.GetOrAdd("S-1", () => { calls++; return "乙"; }));
        Assert.Equal("乙", new DisplayNameCache(TimeProvider.System).GetOrAdd("S-1", () => { calls++; return "乙"; }));
        Assert.Equal(2, calls);
        for (var i = 0; i < 5000; i++) first.GetOrAdd($"S-{i}", () => "名");
        Assert.Equal("名", first.GetOrAdd("S-4999", () => throw new InvalidOperationException("cached")));
    }
}
