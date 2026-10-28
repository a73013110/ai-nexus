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
    public async Task ManagerScopesComeFromEachGrantAndCoverOnlyTheUnitsTheyManage()
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

        // Seed 的 workspace 給 own＋managed-tree：加上所管單位與有效下層，不含停用單位與人資處。
        var managed = await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default);
        Assert.Equal(new HashSet<string> { DataScopes.Own, DataScopes.ManagedTree }, managed.Scopes);
        Assert.Equal(new HashSet<Guid> { it, systems }, managed.UnitIds);
        // 給畫面的代碼與 ForAsync 一致（由窄到寬），沒有範圍的功能不列。
        var codes = await scopes.ScopesAsync(bob, default);
        Assert.Equal([DataScopes.Own, DataScopes.ManagedTree], codes[FeatureCatalog.AttendanceRecords]);
        Assert.DoesNotContain(FeatureCatalog.Audit, codes.Keys);

        // 逐群組設定：managed 只含所管單位本身；不另外時只剩成員範圍。
        await SetRecordsAsync(db, BuiltInAccess.WorkspaceGroup, DataScopes.Own, DataScopes.Managed);
        Assert.Equal(new HashSet<Guid> { it }, (await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default)).UnitIds);
        await SetRecordsAsync(db, BuiltInAccess.WorkspaceGroup, DataScopes.Own, null);
        var off = await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default);
        Assert.Equal(new HashSet<string> { DataScopes.Own }, off.Scopes);
        Assert.Empty(off.UnitIds);

        // 多群組以 OR 合併：另一個群組給 unit＋managed-tree，與 workspace 的 own 合起來。
        db.Set<Role>().Add(new() { Id = "unit-lead", Name = "單位主管" });
        db.Set<RoleGroup>().Add(new() { Id = "unit-leads", Name = "單位主管" });
        db.Set<RoleGroupRole>().Add(new() { RoleId = "unit-lead", GroupId = "unit-leads" });
        db.Set<RoleGroupFeature>().Add(new() { GroupId = "unit-leads", FeatureId = FeatureCatalog.AttendanceRecords, Scope = DataScopes.Unit, ManagerScope = DataScopes.ManagedTree });
        db.Set<UserRole>().Add(new() { UserId = bob, RoleId = "unit-lead" });
        await db.SaveChangesAsync();
        var merged = await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default);
        Assert.Equal(new HashSet<string> { DataScopes.Own, DataScopes.Unit, DataScopes.ManagedTree }, merged.Scopes);
        Assert.Equal(new HashSet<Guid> { it, systems, hr }, merged.UnitIds);

        // 範圍是 all 時不另外計算主管範圍：結果只有成員範圍、不算單位。
        await SetRecordsAsync(db, "unit-leads", DataScopes.All, DataScopes.ManagedTree);
        var all = await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default);
        Assert.Equal(new HashSet<string> { DataScopes.Own, DataScopes.All }, all.Scopes);
        Assert.Empty(all.UnitIds);
        Assert.Equal([DataScopes.Own, DataScopes.All], (await scopes.ScopesAsync(bob, default))[FeatureCatalog.AttendanceRecords]);
        await SetRecordsAsync(db, "unit-leads", DataScopes.Unit, DataScopes.ManagedTree);

        // 不是主管的人拿不到 managed*：取消主管勾選後只剩成員範圍，畫面的代碼也一樣。
        await db.Set<UnitMember>().Where(x => x.UnitId == it && x.UserId == bob).ExecuteUpdateAsync(x => x.SetProperty(y => y.IsManager, false));
        var member = await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default);
        Assert.Equal(new HashSet<string> { DataScopes.Own, DataScopes.Unit }, member.Scopes);
        Assert.Equal(new HashSet<Guid> { it, hr }, member.UnitIds);
        Assert.Equal([DataScopes.Own, DataScopes.Unit], (await scopes.ScopesAsync(bob, default))[FeatureCatalog.AttendanceRecords]);
        await db.Set<UnitMember>().Where(x => x.UnitId == it && x.UserId == bob).ExecuteUpdateAsync(x => x.SetProperty(y => y.IsManager, true));
        await db.Set<UserRole>().Where(x => x.RoleId == "unit-lead").ExecuteDeleteAsync();
        await SetRecordsAsync(db, BuiltInAccess.WorkspaceGroup, DataScopes.Own, DataScopes.ManagedTree);

        // 成員範圍沒有可用值時只靠主管範圍；單位停用後連同下層不算，沒有有效的所管單位就沒有範圍。
        await SetRecordsAsync(db, BuiltInAccess.WorkspaceGroup, null, DataScopes.ManagedTree);
        var only = await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default);
        Assert.Equal(new HashSet<string> { DataScopes.ManagedTree }, only.Scopes);
        Assert.Equal(new HashSet<Guid> { it, systems }, only.UnitIds);
        await db.Set<Unit>().Where(x => x.Id == it).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        Assert.True((await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default)).IsEmpty);
        Assert.DoesNotContain(FeatureCatalog.AttendanceRecords, (await scopes.ScopesAsync(bob, default)).Keys);
        await db.Set<Unit>().Where(x => x.Id == it).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, true));

        // 目錄沒宣告主管範圍的功能，資料庫裡的值（舊資料）也不算。
        db.Set<RoleGroupFeature>().Add(new() { GroupId = BuiltInAccess.WorkspaceGroup, FeatureId = FeatureCatalog.Audit, Scope = DataScopes.Own, ManagerScope = DataScopes.ManagedTree });
        await db.SaveChangesAsync();
        var audit = await scopes.ForAsync(bob, FeatureCatalog.Audit, default);
        Assert.Equal(new HashSet<string> { DataScopes.Own }, audit.Scopes);
        Assert.Empty(audit.UnitIds);

        // 主管範圍不授予功能：沒有授權就沒有範圍；帳號停用也一樣。
        await db.Users.Where(x => x.Id == bob).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        Assert.True((await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default)).IsEmpty);
        await db.Users.Where(x => x.Id == bob).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, true));
        await db.Set<RoleGroupFeature>().Where(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == FeatureCatalog.AttendanceRecords).ExecuteDeleteAsync();
        Assert.True((await scopes.ForAsync(bob, FeatureCatalog.AttendanceRecords, default)).IsEmpty);
    }

    [Fact]
    public async Task HeadUnitManagersGovernTheirParentExceptHigherRankedHeads()
    {
        await using var factory = new AppFactory();
        Guid carol = await UserIdAsync(factory, "carol"), bob = await UserIdAsync(factory, "bob"), dave = await UserIdAsync(factory, "dave"),
            erin = await UserIdAsync(factory, "erin"), frank = await UserIdAsync(factory, "frank");
        Guid company = Guid.NewGuid(), board = Guid.NewGuid(), audit = Guid.NewGuid(), office = Guid.NewGuid(), legal = Guid.NewGuid(),
            finance = Guid.NewGuid(), sales = Guid.NewGuid(), branch = Guid.NewGuid(), closed = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // 公文系統常見的扁平結構：董事會、總經理與各處都在公司底下同一層，以首長單位標出誰管誰。
        db.AddRange(
            new Unit { Id = company, Name = "總公司", SortOrder = 10 },
            new Unit { Id = board, ParentId = company, Name = "董事會", SortOrder = 10, HeadRank = 1 },
            new Unit { Id = audit, ParentId = board, Name = "稽核部", SortOrder = 10 },
            new Unit { Id = office, ParentId = company, Name = "總經理室", SortOrder = 20, HeadRank = 2 },
            new Unit { Id = legal, ParentId = office, Name = "法務部", SortOrder = 10 },
            new Unit { Id = finance, ParentId = company, Name = "財務處", SortOrder = 30 },
            new Unit { Id = sales, ParentId = company, Name = "業務處", SortOrder = 40 },
            new Unit { Id = branch, ParentId = sales, Name = "北區", SortOrder = 10 },
            new Unit { Id = closed, ParentId = company, Name = "已裁撤", Enabled = false, SortOrder = 50 });
        await db.SaveChangesAsync();
        db.AddRange(
            new UnitMember { UnitId = board, UserId = carol, IsPrimary = true, IsManager = true },
            new UnitMember { UnitId = office, UserId = bob, IsPrimary = true, IsManager = true },
            new UnitMember { UnitId = office, UserId = erin, IsPrimary = true },
            new UnitMember { UnitId = finance, UserId = dave, IsPrimary = true, IsManager = true },
            // 上層有首長單位時，它自己的主管勾選不生效（正常流程會被拒絕或清除，這裡直接寫入驗證失敗即拒絕）。
            new UnitMember { UnitId = company, UserId = frank, IsPrimary = true, IsManager = true });
        await db.SaveChangesAsync();
        var scopes = scope.ServiceProvider.GetRequiredService<IDataScope>();
        async Task<IReadOnlySet<Guid>> UnitsAsync(Guid user) => (await scopes.ForAsync(user, FeatureCatalog.AttendanceRecords, default)).UnitIds;

        // 董事長管整間公司；總經理管公司與層級不比它高的單位，看不到董事會與其下的稽核部；一般單位主管照舊。
        Assert.Equal(new HashSet<Guid> { company, board, audit, office, legal, finance, sales, branch }, await UnitsAsync(carol));
        Assert.Equal(new HashSet<Guid> { company, office, legal, finance, sales, branch }, await UnitsAsync(bob));
        Assert.Equal(new HashSet<Guid> { finance }, await UnitsAsync(dave));
        Assert.Empty(await UnitsAsync(frank));
        Assert.Equal([DataScopes.Own], (await scopes.ScopesAsync(frank, default))[FeatureCatalog.AttendanceRecords]);

        // managed 只含任主管的單位：首長單位加上它的上層（公司的直屬成員），不展開各處。
        await SetRecordsAsync(db, BuiltInAccess.WorkspaceGroup, DataScopes.UnitTree, DataScopes.Managed);
        Assert.Equal(new HashSet<Guid> { company, office, legal }, await UnitsAsync(bob));
        // 成員範圍只看單位樹：總經理室的成員選所屬單位及下層，不會因首長單位看到全公司。
        Assert.Equal(new HashSet<Guid> { office, legal }, await UnitsAsync(erin));
        await SetRecordsAsync(db, BuiltInAccess.WorkspaceGroup, DataScopes.Own, DataScopes.ManagedTree);

        // 董事會停用：董事長沒有範圍，總經理照常（失敗即拒絕，不改由其他人遞補）。
        await db.Set<Unit>().Where(x => x.Id == board).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        Assert.Empty(await UnitsAsync(carol));
        Assert.Equal(new HashSet<Guid> { company, office, legal, finance, sales, branch }, await UnitsAsync(bob));
        await db.Set<Unit>().Where(x => x.Id == board).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, true));

        // 董事會不再是首長單位：它成了一般的處，總經理也管得到；公司仍由總經理室帶領，主管勾選照樣不生效。
        await db.Set<Unit>().Where(x => x.Id == board).ExecuteUpdateAsync(x => x.SetProperty(y => y.HeadRank, (byte?)null));
        Assert.Equal(new HashSet<Guid> { company, board, audit, office, legal, finance, sales, branch }, await UnitsAsync(bob));
        Assert.Equal(new HashSet<Guid> { board, audit }, await UnitsAsync(carol));
        Assert.Empty(await UnitsAsync(frank));
    }

    private static Task SetRecordsAsync(AppDbContext db, string group, string? scope, string? manager) => db.Set<RoleGroupFeature>()
        .Where(x => x.GroupId == group && x.FeatureId == FeatureCatalog.AttendanceRecords)
        .ExecuteUpdateAsync(x => x.SetProperty(y => y.Scope, scope).SetProperty(y => y.ManagerScope, manager));
}
