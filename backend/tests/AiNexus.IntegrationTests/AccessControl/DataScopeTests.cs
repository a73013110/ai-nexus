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
        Assert.Equal([DataScopes.UnitTree, DataScopes.All], (await scopes.ScopesAsync(bob, default))[FeatureCatalog.Audit]);

        // 群組停用、功能停用都等於沒有範圍；沒有宣告範圍的功能不能查範圍。
        await db.Set<RoleGroup>().Where(x => x.Id == "self-review").ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        Assert.Equal(new HashSet<string> { DataScopes.UnitTree }, (await scopes.ForAsync(bob, FeatureCatalog.Audit, default)).Scopes);
        await db.Set<Feature>().Where(x => x.Id == FeatureCatalog.Audit).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        Assert.True((await scopes.ForAsync(bob, FeatureCatalog.Audit, default)).IsEmpty);
        await Assert.ThrowsAsync<ArgumentException>(() => scopes.ForAsync(bob, FeatureCatalog.Chat, default));
    }

    [Fact]
    public async Task ManagersGetTheDeclaredScopeOverTheUnitsTheyManageOnly()
    {
        await using var factory = new AppFactory();
        var bob = await UserIdAsync(factory, "bob");
        Guid it = Guid.NewGuid(), systems = Guid.NewGuid(), closed = Guid.NewGuid(), hr = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(
            new Unit { Id = it, Name = "資訊處", SortOrder = 10 },
            new Unit { Id = systems, ParentId = it, Name = "系統部", SortOrder = 10 },
            new Unit { Id = closed, ParentId = it, Name = "已裁撤", Enabled = false, SortOrder = 20 },
            new Unit { Id = hr, Name = "人資處", SortOrder = 20 });
        await db.SaveChangesAsync();
        // bob 是資訊處主管，同時掛在人資處但不是主管。
        db.AddRange(
            new UnitMember { UnitId = it, UserId = bob, IsPrimary = true, IsManager = true },
            new UnitMember { UnitId = hr, UserId = bob });
        await db.SaveChangesAsync();
        var scopes = scope.ServiceProvider.GetRequiredService<IDataScope>();

        // Seed 的 workspace 給 own；主管範圍 unit-tree 加上所管單位與有效下層，不含停用單位與人資處。
        var managed = await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default);
        Assert.Equal(new HashSet<string> { DataScopes.Own, DataScopes.UnitTree }, managed.Scopes);
        Assert.Equal(new HashSet<Guid> { it, systems }, managed.UnitIds);
        // 給畫面的代碼與 ForAsync 一致（由窄到寬），沒有範圍的功能不列。
        var codes = await scopes.ScopesAsync(bob, default);
        Assert.Equal([DataScopes.Own, DataScopes.UnitTree], codes[FeatureCatalog.AttendanceRecords]);
        Assert.DoesNotContain(FeatureCatalog.Audit, codes.Keys);

        // 沒宣告主管範圍的功能不受影響（bob 預設沒有稽核授權；給 own 也只有 own）。
        Assert.True((await scopes.ForAsync(bob, FeatureCatalog.Audit, default)).IsEmpty);
        db.Set<RoleGroupFeature>().Add(new() { GroupId = BuiltInAccess.WorkspaceGroup, FeatureId = FeatureCatalog.Audit, Scope = DataScopes.Own });
        await db.SaveChangesAsync();
        var audit = await scopes.ForAsync(bob, FeatureCatalog.Audit, default);
        Assert.Equal(new HashSet<string> { DataScopes.Own }, audit.Scopes);
        Assert.Empty(audit.UnitIds);

        // 與群組範圍以 OR 合併：群組的 unit 帶進人資處。
        await SetRecordsScopeAsync(db, DataScopes.Unit);
        Assert.Equal(new HashSet<Guid> { it, systems, hr }, (await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default)).UnitIds);

        // 群組沒有可用範圍時只靠主管範圍；功能授權本身不能少。
        await SetRecordsScopeAsync(db, null);
        var only = await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default);
        Assert.Equal(new HashSet<string> { DataScopes.UnitTree }, only.Scopes);
        Assert.Equal(new HashSet<Guid> { it, systems }, only.UnitIds);

        // 單位停用後連同下層不算；沒有有效的所管單位時就沒有範圍。
        await db.Set<Unit>().Where(x => x.Id == it).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        Assert.True((await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default)).IsEmpty);
        await db.Set<Unit>().Where(x => x.Id == it).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, true));

        // 取消主管勾選就失效。
        await db.Set<UnitMember>().Where(x => x.UnitId == it && x.UserId == bob).ExecuteUpdateAsync(x => x.SetProperty(y => y.IsManager, false));
        Assert.True((await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default)).IsEmpty);
        Assert.DoesNotContain(FeatureCatalog.AttendanceRecords, (await scopes.ScopesAsync(bob, default)).Keys);
        await db.Set<UnitMember>().Where(x => x.UnitId == it && x.UserId == bob).ExecuteUpdateAsync(x => x.SetProperty(y => y.IsManager, true));

        // 主管範圍不授予功能：沒有授權就沒有範圍；帳號停用也一樣。
        await db.Users.Where(x => x.Id == bob).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        Assert.True((await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default)).IsEmpty);
        await db.Users.Where(x => x.Id == bob).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, true));
        await db.Set<RoleGroupFeature>().Where(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == FeatureCatalog.AttendanceRecords).ExecuteDeleteAsync();
        Assert.True((await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default)).IsEmpty);
    }

    private static Task SetRecordsScopeAsync(AppDbContext db, string? value) => db.Set<RoleGroupFeature>()
        .Where(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == FeatureCatalog.AttendanceRecords)
        .ExecuteUpdateAsync(x => x.SetProperty(y => y.Scope, value));
}
