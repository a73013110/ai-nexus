using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.OrganizationApi;

namespace AiNexus.IntegrationTests.AccessControl;

/// <summary><see cref="IDataScope"/>：範圍來自有效群組的授權（OR 合併），單位由 Organization 計算（ADR 0017）。</summary>
public sealed class DataScopeTests
{
    [Fact]
    public async Task ScopesMergeAcrossGroupsAndUnitsFollowTheEnabledTree()
    {
        await using var factory = new AppFactory();
        var bob = await UserIdAsync(factory, "bob");
        Guid it = Guid.NewGuid(), systems = Guid.NewGuid(), closed = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(
            new Unit { Id = it, Name = "資訊處", SortOrder = 10 },
            new Unit { Id = systems, ParentId = it, Name = "系統部", SortOrder = 10 },
            new Unit { Id = closed, ParentId = it, Name = "已裁撤", Enabled = false, SortOrder = 20 });
        await db.SaveChangesAsync();
        db.Add(new UnitMember { UnitId = it, UserId = bob, IsPrimary = true });
        db.Set<Role>().Add(new() { Id = "reviewer", Name = "稽核查閱" });
        db.Set<RoleGroup>().AddRange(new RoleGroup { Id = "unit-reviewers", Name = "單位" }, new RoleGroup { Id = "self-review", Name = "本人" });
        db.Set<RoleGroupRole>().AddRange(new RoleGroupRole { RoleId = "reviewer", GroupId = "unit-reviewers" }, new RoleGroupRole { RoleId = "reviewer", GroupId = "self-review" });
        db.Set<RoleGroupFeature>().AddRange(
            new RoleGroupFeature { GroupId = "unit-reviewers", FeatureId = FeatureCatalog.Audit, Scope = DataScopes.Unit },
            new RoleGroupFeature { GroupId = "self-review", FeatureId = FeatureCatalog.Audit, Scope = DataScopes.Own });
        db.Set<UserRole>().Add(new() { UserId = bob, RoleId = "reviewer" });
        await db.SaveChangesAsync();
        var scopes = scope.ServiceProvider.GetRequiredService<IDataScope>();

        var unit = await scopes.ForAsync(bob, FeatureCatalog.Audit, default);
        Assert.Equal(new HashSet<string> { DataScopes.Own, DataScopes.Unit }, unit.Scopes);
        Assert.True(unit.Own); Assert.False(unit.All);
        Assert.Equal(new HashSet<Guid> { it }, unit.UnitIds);

        await db.Set<RoleGroupFeature>().Where(x => x.GroupId == "unit-reviewers").ExecuteUpdateAsync(x => x.SetProperty(y => y.Scope, DataScopes.UnitTree));
        Assert.Equal(new HashSet<Guid> { it, systems }, (await scopes.ForAsync(bob, FeatureCatalog.Audit, default)).UnitIds);

        await db.Set<RoleGroupFeature>().Where(x => x.GroupId == "self-review").ExecuteUpdateAsync(x => x.SetProperty(y => y.Scope, DataScopes.All));
        var all = await scopes.ForAsync(bob, FeatureCatalog.Audit, default);
        Assert.True(all.All); Assert.Empty(all.UnitIds);

        // 群組停用、功能停用都等於沒有範圍；沒有宣告範圍的功能不能查範圍。
        await db.Set<RoleGroup>().Where(x => x.Id == "self-review").ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        Assert.Equal(new HashSet<string> { DataScopes.UnitTree }, (await scopes.ForAsync(bob, FeatureCatalog.Audit, default)).Scopes);
        await db.Set<Feature>().Where(x => x.Id == FeatureCatalog.Audit).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        Assert.True((await scopes.ForAsync(bob, FeatureCatalog.Audit, default)).IsEmpty);
        await Assert.ThrowsAsync<ArgumentException>(() => scopes.ForAsync(bob, FeatureCatalog.Chat, default));
    }
}
