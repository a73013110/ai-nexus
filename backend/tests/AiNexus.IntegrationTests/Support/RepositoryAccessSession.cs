using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Work.Repositories;
using AiNexus.Features.Work.Repositories.Access;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 儲存庫權限的測試場景：alice 是管理員、bob 是申請人、dave 是 bob 的主管、carol 是 hanglong/nexus 的負責人。
/// 已同步一輪目錄；登入名＝帳號名。預設流程（Seed/flow.sql）是主管、再負責人。
/// </summary>
internal sealed class RepositoryAccessSession : IAsyncDisposable
{
    public const string Repositories = "/api/v1/managed-repositories";
    public const string Nexus = "hanglong/nexus";

    // 詳情帶列舉（管理身分），API 以字串輸出。
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private RepositoryAccessSession(AppFactory factory, FakeGiteaService gitea) => (Factory, Gitea) = (factory, gitea);

    public AppFactory Factory { get; }
    public FakeGiteaService Gitea { get; }
    public Dictionary<string, Guid> Users { get; } = [];
    public Guid Repository { get; private set; }

    public static async Task<RepositoryAccessSession> StartAsync(TimeProvider? clock = null)
    {
        var gitea = new FakeGiteaService();
        var factory = new AppFactory(administrators: ["alice"], clock: clock, services: services =>
        {
            services.RemoveAll<IGiteaServiceClient>();
            services.AddSingleton<IGiteaServiceClient>(gitea);
            services.PostConfigure<GiteaOptions>(x => { x.Enabled = true; x.ServiceToken = "fixture-service-token"; });
        });
        var session = new RepositoryAccessSession(factory, gitea);
        foreach (var name in new[] { "alice", "bob", "carol", "dave" })
            session.Users[name] = await OrganizationApi.UserIdAsync(factory, name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var (name, id) in session.Users)
            await db.Users.Where(x => x.Id == id).ExecuteUpdateAsync(x => x.SetProperty(y => y.AdAccount, name));
        var unit = Guid.NewGuid();
        db.Units.Add(new Unit { Id = unit, Name = "工程部", SortOrder = 10 });
        await db.SaveChangesAsync();
        db.UnitMembers.AddRange(
            new UnitMember { UnitId = unit, UserId = session.Users["dave"], IsPrimary = true, IsManager = true },
            new UnitMember { UnitId = unit, UserId = session.Users["bob"], IsPrimary = true });
        await db.SaveChangesAsync();

        await session.SyncAsync();
        session.Repository = await db.ManagedRepositories.Where(x => x.Name == "nexus").Select(x => x.Id).SingleAsync();
        db.RepositoryStewards.Add(new RepositorySteward { RepositoryId = session.Repository, UserId = session.Users["carol"] });
        await db.SaveChangesAsync();
        return session;
    }

    public Task<HttpClient> ClientAsync(string name) => Factory.SignedInAsync(name);

    /// <summary>跑一輪同步任務（目錄、到期、收斂）。</summary>
    public Task<TaskOutcome> SyncAsync() => Factory.InScopeAsync((RepositoryAccessSyncTask task)
        => task.RunAsync(new ScheduledTaskContext(ScheduledTaskTrigger.Manual, null, null), CancellationToken.None));

    public async Task<ManagedRepositoryDetailDto> DetailAsync(HttpClient client)
        => (await client.GetFromJsonAsync<ManagedRepositoryDetailDto>($"{Repositories}/{Repository}", Json))!;

    public Task<HttpResponseMessage> RequestAsync(HttpClient client, string permission, DateOnly? expiresOn = null)
        => client.PostAsJsonAsync($"{Repositories}/{Repository}/requests", new RepositoryAccessRequestBody(permission, expiresOn, "開發新功能需要"));

    public Task<HttpResponseMessage> GrantAsync(HttpClient client, Guid? user, string permission, string? login = null, string? reason = null)
        => client.PostAsJsonAsync($"{Repositories}/{Repository}/grants", new RepositoryGrantRequest(user, login, permission, null, reason));

    public async Task<RepositoryGrant[]> GrantsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return [.. await scope.ServiceProvider.GetRequiredService<AppDbContext>().RepositoryGrants.AsNoTracking().OrderBy(x => x.GrantedAt).ToListAsync()];
    }

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
