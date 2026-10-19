using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.OrganizationApi;

namespace AiNexus.IntegrationTests.Audit;

/// <summary>活動稽核是第一個套用資料範圍的功能（ADR 0017）：列表與匯出只含範圍內的人（操作者或對象）的紀錄。</summary>
public sealed class AuditDataScopeTests
{
    private const string Action = "fixture.scope";

    [Fact]
    public async Task ListAndExportOnlyShowRecordsOfPeopleInTheGrantedScope()
    {
        await using var factory = new AppFactory();
        Guid bob = await UserIdAsync(factory, "bob"), frank = await UserIdAsync(factory, "frank"), carol = await UserIdAsync(factory, "carol"),
            erin = await UserIdAsync(factory, "erin"), dave = await UserIdAsync(factory, "dave");
        var system = Guid.NewGuid();
        Guid company = Guid.NewGuid(), it = Guid.NewGuid(), systems = Guid.NewGuid(), closed = Guid.NewGuid(), hr = Guid.NewGuid();
        await WithDbAsync(factory, async db =>
        {
            db.AddRange(
                new Unit { Id = company, Name = "總公司", SortOrder = 10 },
                new Unit { Id = it, ParentId = company, Name = "資訊處", SortOrder = 10 },
                new Unit { Id = systems, ParentId = it, Name = "系統部", SortOrder = 10 },
                new Unit { Id = closed, ParentId = it, Name = "已裁撤", Enabled = false, SortOrder = 20 },
                new Unit { Id = hr, ParentId = company, Name = "人資處", SortOrder = 20 });
            await db.SaveChangesAsync();
            db.AddRange(
                new UnitMember { UnitId = it, UserId = bob, IsPrimary = true, IsManager = true },
                new UnitMember { UnitId = it, UserId = frank, IsPrimary = true },
                new UnitMember { UnitId = systems, UserId = carol, IsPrimary = true },
                new UnitMember { UnitId = closed, UserId = erin, IsPrimary = true },
                new UnitMember { UnitId = hr, UserId = dave, IsPrimary = true });
            // ResourceId 標記紀錄屬於誰；dave 代 bob 操作的紀錄，操作者在範圍內就看得到。
            db.AuditEvents.AddRange(new[] { bob, frank, carol, erin, dave, system }.Select(owner => new AuditEvent { OwnerId = owner, Action = Action, ResourceId = owner, Result = "saved" }));
            db.AuditEvents.Add(new AuditEvent { OwnerId = dave, ActorId = bob, Action = Action, ResourceId = Guid.Empty, Result = "saved" });
            db.Set<Role>().Add(new() { Id = "reviewer", Name = "稽核查閱" });
            db.Set<RoleGroup>().AddRange(new RoleGroup { Id = "reviewers", Name = "稽核查閱" }, new RoleGroup { Id = "self-review", Name = "自己的紀錄" });
            db.Set<RoleGroupRole>().Add(new() { RoleId = "reviewer", GroupId = "reviewers" });
            db.Set<RoleGroupFeature>().Add(new() { GroupId = "reviewers", FeatureId = FeatureCatalog.Audit, Scope = DataScopes.Unit });
            db.Set<UserRole>().Add(new() { UserId = bob, RoleId = "reviewer" });
            await db.SaveChangesAsync();
        });
        using var client = await factory.SignedInAsync("bob");

        Assert.Equal([bob, frank, Guid.Empty], await VisibleAsync(client));
        await SetScopeAsync(factory, DataScopes.UnitTree);
        Assert.Equal([bob, frank, carol, Guid.Empty], await VisibleAsync(client));
        await SetScopeAsync(factory, DataScopes.Own);
        Assert.Equal([bob, Guid.Empty], await VisibleAsync(client));

        // 多個群組的範圍以 OR 合併。
        await WithDbAsync(factory, async db =>
        {
            db.Set<RoleGroupRole>().Add(new() { RoleId = "reviewer", GroupId = "self-review" });
            db.Set<RoleGroupFeature>().Add(new() { GroupId = "self-review", FeatureId = FeatureCatalog.Audit, Scope = DataScopes.UnitTree });
            await db.SaveChangesAsync();
        });
        Assert.Equal([bob, frank, carol, Guid.Empty], await VisibleAsync(client));
        await WithDbAsync(factory, db => db.Set<RoleGroupRole>().Where(x => x.GroupId == "self-review").ExecuteDeleteAsync());

        await SetScopeAsync(factory, DataScopes.All);
        Assert.Equal([bob, frank, carol, erin, dave, system, Guid.Empty], await VisibleAsync(client));

        // 授權在、但沒有可用的範圍（舊資料的 null、目錄不支援的值）：頁面能開，沒有資料。
        await SetScopeAsync(factory, null);
        Assert.Empty(await VisibleAsync(client));
        await SetScopeAsync(factory, DataScopes.Assigned);
        Assert.Empty(await VisibleAsync(client));

        await SetScopeAsync(factory, DataScopes.Unit);
        var csv = await client.GetStringAsync($"/api/v1/admin/audit/export?action={Action}");
        Assert.Contains(frank.ToString(), csv);
        Assert.DoesNotContain(carol.ToString(), csv);
        Assert.DoesNotContain(system.ToString(), csv);

        // 撤銷授權仍是 403，與範圍無關。
        await WithDbAsync(factory, db => db.Set<UserRole>().Where(x => x.UserId == bob && x.RoleId == "reviewer").ExecuteDeleteAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/audit")).StatusCode);
    }

    [Fact]
    public async Task UnitScopeWithoutAnyMembershipShowsNothing()
    {
        await using var factory = new AppFactory();
        var bob = await UserIdAsync(factory, "bob");
        var dave = await UserIdAsync(factory, "dave");
        await WithDbAsync(factory, async db =>
        {
            db.AuditEvents.AddRange(new[] { bob, dave }.Select(owner => new AuditEvent { OwnerId = owner, Action = Action, ResourceId = owner, Result = "saved" }));
            db.Set<Role>().Add(new() { Id = "reviewer", Name = "稽核查閱" });
            db.Set<RoleGroup>().Add(new() { Id = "reviewers", Name = "稽核查閱" });
            db.Set<RoleGroupRole>().Add(new() { RoleId = "reviewer", GroupId = "reviewers" });
            db.Set<RoleGroupFeature>().Add(new() { GroupId = "reviewers", FeatureId = FeatureCatalog.Audit, Scope = DataScopes.UnitTree });
            db.Set<UserRole>().Add(new() { UserId = bob, RoleId = "reviewer" });
            await db.SaveChangesAsync();
        });
        using var client = await factory.SignedInAsync("bob");
        Assert.Empty(await VisibleAsync(client));
    }

    private static async Task<Guid[]> VisibleAsync(HttpClient client)
        => [.. (await client.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?action={Action}"))!.OrderBy(x => x.Id).Select(x => x.ResourceId!.Value)];

    private static Task SetScopeAsync(AppFactory factory, string? scope)
        => WithDbAsync(factory, db => db.Set<RoleGroupFeature>().Where(x => x.GroupId == "reviewers").ExecuteUpdateAsync(x => x.SetProperty(y => y.Scope, scope)));

    private static async Task WithDbAsync(AppFactory factory, Func<AppDbContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
