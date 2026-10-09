using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using AiNexus.Features.Account;
using AiNexus.Features.Chat;
using AiNexus.Features.Identity;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Integrations;
using EDoc.Core.Database.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AiNexus.Tests;
public sealed class IntegrationTests
{
    private static async Task<HttpClient> SignIn(WebApplicationFactory<Program> f, string name)
    {
        var client = f.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-User", name);
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!; client.DefaultRequestHeaders.Add("X-Nexus-CSRF", me.CsrfToken); return client;
    }
    [Fact]
    public async Task ServerIdentityAndSourceAclProtectQueriesAndImportedSnapshotsStayPrivate()
    {
        await using var baseFactory = new NexusFactory(db => db.Add(new RoleGroupFeature { GroupId = BuiltInAccess.WorkspaceGroup, FeatureId = "integrations" }));
        var adapter = new FixtureSource();
        await using var f = baseFactory.WithWebHostBuilder(b => {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:LegacyGdweb"] = "fixture-only" }));
            b.ConfigureServices(s => { s.RemoveAll<IControlledSourceAdapter>(); s.AddSingleton<IControlledSourceAdapter>(adapter); s.PostConfigure<IntegrationsOptions>(o => { o.Gdweb.Enabled = true; o.Gdweb.AclContractConfirmed = true; o.Gdweb.AllowedGroupIds = ["workspace"]; }); });
        });
        using var owner = await SignIn(f, "alice"); using var other = await SignIn(f, "bob");
        var list = await owner.GetFromJsonAsync<List<SourceRecordDto>>("/api/v1/integrations/gdweb/records?query=通知&actorSid=spoof"); Assert.Single(list!); Assert.Equal("S-1-5-21-test-alice", adapter.Actor!.Sid); Assert.Equal("TEST\\alice", adapter.Actor.Account);
        Assert.Empty((await other.GetFromJsonAsync<List<SourceRecordDto>>("/api/v1/integrations/gdweb/records?query=通知"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/v1/integrations/gdweb/record?id=doc-1")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/v1/integrations/gdweb/import", new SourceImportRequest("doc-1", "old"))).StatusCode);
        var response = await owner.PostAsJsonAsync("/api/v1/integrations/gdweb/import", new SourceImportRequest("doc-1", "v2")); response.EnsureSuccessStatusCode(); var artifact = (await response.Content.ReadFromJsonAsync<ArtifactDto>())!;
        Assert.Contains("當時快照", artifact.Content); Assert.Contains("v2", artifact.Content); Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/artifacts/{artifact.Resource.Id}")).StatusCode);
        var chatResponse = await owner.PostAsJsonAsync("/api/v1/integrations/gdweb/chat", new SourceImportRequest("doc-1", "v2")); chatResponse.EnsureSuccessStatusCode();
        var draft = (await chatResponse.Content.ReadFromJsonAsync<SourceChatDto>())!; Assert.Contains("通知正文", draft.Prompt); Assert.Empty((await owner.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{draft.Conversation.Id}"))!.Messages); Assert.Equal(0, baseFactory.Provider.Calls);
        adapter.Revoked = true; Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/v1/integrations/gdweb/record?id=doc-1")).StatusCode);
        Assert.Contains("通知正文", (await owner.GetFromJsonAsync<ArtifactDto>($"/api/v1/artifacts/{artifact.Resource.Id}"))!.Content);
    }
    [Fact]
    public async Task UnconfirmedAclAndMissingSourceGroupsNeverInvokeTheAdapter()
    {
        await using var baseFactory = new NexusFactory(db => db.Add(new RoleGroupFeature { GroupId = BuiltInAccess.WorkspaceGroup, FeatureId = "integrations" })); var adapter = new FixtureSource();
        await using var f = baseFactory.WithWebHostBuilder(b => { b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:LegacyGdweb"] = "fixture-only" })); b.ConfigureServices(s => { s.RemoveAll<IControlledSourceAdapter>(); s.AddSingleton<IControlledSourceAdapter>(adapter); s.PostConfigure<IntegrationsOptions>(o => { o.Gdweb.Enabled = true; o.Gdweb.AclContractConfirmed = true; o.Gdweb.AllowedGroupIds = []; }); }); });
        using var owner = await SignIn(f, "alice"); Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/v1/integrations/gdweb/records?query=通知")).StatusCode); Assert.Equal(0, adapter.Calls);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await owner.GetAsync("/api/v1/integrations/meiho/records?query=規範")).StatusCode);
        Assert.Equal("not-authorized", (await owner.GetFromJsonAsync<List<SourceDto>>("/api/v1/integrations"))!.Single(x => x.Id == "gdweb").Status);
    }
    [Fact]
    public async Task SqlAdapterKeepsSearchValuesInParametersAndRechecksGrantAfterHistory()
    {
        var helper = DispatchProxy.Create<IDbHelper<ILegacyGdwebDatabase>, QueryProbe>(); var probe = (QueryProbe)(object)helper; var adapter = new GdwebSource(helper);
        await adapter.SearchAsync(new("sid-123", "DOMAIN\\actor"), new("' OR 1=1 --%_"), 30, 10, CancellationToken.None);
        Assert.DoesNotContain("OR 1=1", probe.Sql); Assert.Contains("ActorSid = @ActorSid", probe.Sql); Assert.Equal("%' OR 1=1 --~%~_%", probe.Args!.GetType().GetProperty("Pattern")!.GetValue(probe.Args));
        Assert.Equal(30, probe.Args.GetType().GetProperty("Take")!.GetValue(probe.Args));
        probe.RevokeOnRecheck = true; Assert.Null(await adapter.ReadAsync(new("sid-123", "DOMAIN\\actor"), "doc-1", 10, CancellationToken.None)); Assert.Equal(2, probe.SingleReads);
    }
    private sealed class FixtureSource : IControlledSourceAdapter
    {
        public string Id => "gdweb"; public SourceActor? Actor; public int Calls; public bool Revoked;
        private bool Allow(SourceActor actor) { Actor = actor; Calls++; return actor.Sid == "S-1-5-21-test-alice" && !Revoked; }
        public Task<IReadOnlyList<SourceRecordDto>> SearchAsync(SourceActor actor, SourceSearchRequest request, int take, int timeout, CancellationToken ct) => Task.FromResult<IReadOnlyList<SourceRecordDto>>(Allow(actor) ? [Record()] : []);
        public Task<SourceDetailDto?> ReadAsync(SourceActor actor, string id, int timeout, CancellationToken ct) => Task.FromResult<SourceDetailDto?>(Allow(actor) && id == "doc-1" ? new("gdweb", Record(), "通知正文", [new(DateTimeOffset.UtcNow, "approved", "承辦人", "完成核對", "v2")], false) : null);
        private static SourceRecordDto Record() => new("doc-1", "document", "測試通知", "核准", "v2", DateTimeOffset.UtcNow);
    }
}
public class QueryProbe : DispatchProxy
{
    public string Sql = ""; public object? Args; public bool RevokeOnRecheck; public int SingleReads;
    protected override object? Invoke(MethodInfo? target, object?[]? args)
    {
        Sql = (string)args![0]!; Args = args[1];
        if (target!.Name == "QueryAsync" && target.GetGenericArguments()[0] == typeof(SourceRow)) return Task.FromResult<IEnumerable<SourceRow>>([]);
        if (target.Name == "QueryAsync") return Task.FromResult<IEnumerable<SourceHistoryDto>>([]);
        SingleReads++; return Task.FromResult<SourceRow?>(RevokeOnRecheck && SingleReads > 1 ? null : new SourceRow { RecordId = "doc-1", Title = "測試", Body = "測試資料", Revision = "v1" });
    }
}
