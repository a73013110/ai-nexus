using System.Net;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.RecordsSession;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>
/// 出勤紀錄依 <c>attendance.records</c> 的資料範圍決定看得到誰（ADR 0018）：own、unit、unit-tree、all，多群組以 OR 合併；
/// 組織勾選的主管另外自動看得到所管單位含下層（目錄的主管範圍，ADR 0017）。
/// </summary>
public sealed class AttendanceRecordsScopeTests
{
    [Fact]
    public async Task EachScopeShowsOnlyThePeopleItCovers()
    {
        await using var session = Create();
        Guid bob = await session.PersonAsync("bob"), frank = await session.PersonAsync("frank"), carol = await session.PersonAsync("carol"),
            erin = await session.PersonAsync("erin"), dave = await session.PersonAsync("dave");
        Guid company = Guid.NewGuid(), it = Guid.NewGuid(), systems = Guid.NewGuid(), closed = Guid.NewGuid(), hr = Guid.NewGuid();
        await session.WithDbAsync(async db =>
        {
            db.AddRange(
                new Unit { Id = company, Name = "總公司", SortOrder = 10 },
                new Unit { Id = it, ParentId = company, Name = "資訊處", SortOrder = 10 },
                new Unit { Id = systems, ParentId = it, Name = "系統部", SortOrder = 10 },
                new Unit { Id = closed, ParentId = it, Name = "已裁撤", Enabled = false, SortOrder = 20 },
                new Unit { Id = hr, ParentId = company, Name = "人資處", SortOrder = 20 });
            await db.SaveChangesAsync();
            db.AddRange(
                // 這裡只驗群組範圍：bob 不是主管，否則主管範圍會讓他預設就看到整個資訊處。
                new UnitMember { UnitId = it, UserId = bob, IsPrimary = true },
                new UnitMember { UnitId = it, UserId = frank, IsPrimary = true },
                new UnitMember { UnitId = systems, UserId = carol, IsPrimary = true },
                new UnitMember { UnitId = closed, UserId = erin, IsPrimary = true },
                new UnitMember { UnitId = hr, UserId = dave, IsPrimary = true });
            await db.SaveChangesAsync();
        });
        var cards = 0;
        foreach (var person in new[] { bob, frank, carol, erin, dave }) await session.HoldAsync(person, $"A{++cards}");
        using var client = await session.Factory.SignedInAsync("bob");

        // 一般使用者（Seed 的 workspace）預設只看自己。
        Assert.Equal([bob], await PeopleAsync(client));

        await session.GrantAsync(bob, DataScopes.Unit);
        Assert.Equal(Sorted(bob, frank), await PeopleAsync(client));

        await SetScopeAsync(session, DataScopes.UnitTree);
        // 下層單位的成員看得到；停用單位（連同下層）不算。
        Assert.Equal(Sorted(bob, frank, carol), await PeopleAsync(client));

        await SetScopeAsync(session, DataScopes.All);
        Assert.Equal(Sorted(bob, frank, carol, erin, dave), await PeopleAsync(client));
        // 部門篩選含下層單位，與範圍取交集。
        Assert.Equal(Sorted(bob, frank, carol), await PeopleAsync(client, Week + $"&unitId={it}"));
        Assert.Equal([dave], await PeopleAsync(client, Week + $"&userId={dave}"));

        // 指定範圍外的人只是查不到，不透露他有沒有出勤資料。
        await SetScopeAsync(session, DataScopes.Unit);
        Assert.Empty(await PeopleAsync(client, Week + $"&userId={dave}"));
        Assert.Empty(await PeopleAsync(client, Week + $"&unitId={hr}"));
    }

    [Fact]
    public async Task ManagersSeeTheUnitsTheyManageAndTheirSubunits()
    {
        await using var session = Create(services: RequestQueries.Register);
        Guid bob = await session.PersonAsync("bob"), frank = await session.PersonAsync("frank"), carol = await session.PersonAsync("carol"),
            erin = await session.PersonAsync("erin"), dave = await session.PersonAsync("dave"), grace = await session.PersonAsync("grace");
        Guid it = Guid.NewGuid(), systems = Guid.NewGuid(), closed = Guid.NewGuid(), hr = Guid.NewGuid(), lab = Guid.NewGuid();
        await session.WithDbAsync(async db =>
        {
            db.AddRange(
                new Unit { Id = it, Name = "資訊處", SortOrder = 10 },
                new Unit { Id = systems, ParentId = it, Name = "系統部", SortOrder = 10 },
                new Unit { Id = closed, ParentId = it, Name = "已裁撤", Enabled = false, SortOrder = 20 },
                new Unit { Id = hr, Name = "人資處", SortOrder = 20 },
                new Unit { Id = lab, Name = "研究室", SortOrder = 30 });
            await db.SaveChangesAsync();
            // bob 管資訊處與研究室，同時掛在人資處但不是主管。
            db.AddRange(
                new UnitMember { UnitId = it, UserId = bob, IsPrimary = true, IsManager = true },
                new UnitMember { UnitId = lab, UserId = bob, IsManager = true },
                new UnitMember { UnitId = hr, UserId = bob },
                new UnitMember { UnitId = it, UserId = frank, IsPrimary = true },
                new UnitMember { UnitId = systems, UserId = carol, IsPrimary = true },
                new UnitMember { UnitId = closed, UserId = erin, IsPrimary = true },
                new UnitMember { UnitId = hr, UserId = dave, IsPrimary = true },
                new UnitMember { UnitId = lab, UserId = grace, IsPrimary = true });
            await db.SaveChangesAsync();
        });
        var cards = 0;
        foreach (var person in new[] { bob, frank, carol, erin, dave, grace }) await session.HoldAsync(person, $"A{++cards}");
        using var client = await session.Factory.SignedInAsync("bob");
        var queries = session.Factory.Services.GetRequiredService<RequestQueries>();

        // 預設只有 workspace 的 own，主管範圍再加上所管單位含下層；停用單位與非主管的人資處不算。
        queries.Clear();
        Assert.Equal(Sorted(bob, frank, carol, grace), await PeopleAsync(client));
        // 授權只讀一次；單位樹讀兩次（所屬＋下層），不隨所管單位數增加。
        Assert.Equal(1, queries.Count(Records, "RoleGroupFeatures"));
        Assert.Equal(2, queries.Count(Records, "[organization].[Units]"));

        // 與群組範圍以 OR 合併。
        await session.GrantAsync(bob, DataScopes.Unit);
        Assert.Equal(Sorted(bob, frank, carol, dave, grace), await PeopleAsync(client));
        await session.WithDbAsync(db => db.Set<UserRole>().Where(x => x.UserId == bob && x.RoleId == "test-records-" + DataScopes.Unit).ExecuteDeleteAsync());

        // 取消主管勾選就失效。
        await session.WithDbAsync(db => db.Set<UnitMember>().Where(x => x.UserId == bob && x.UnitId == lab)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsManager, false)));
        Assert.Equal(Sorted(bob, frank, carol), await PeopleAsync(client));

        // 主管範圍不授予功能：沒有授權仍是 403。
        await session.WithDbAsync(db => db.Set<RoleGroupFeature>()
            .Where(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == FeatureCatalog.AttendanceRecords).ExecuteDeleteAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Records}?{Week}")).StatusCode);
    }

    [Fact]
    public async Task AGrantWithoutAUsableScopeShowsNobody()
    {
        await using var session = Create();
        var bob = await session.PersonAsync("bob");
        await session.HoldAsync(bob, "A1");
        await session.WithDbAsync(db => db.Set<RoleGroupFeature>()
            .Where(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == FeatureCatalog.AttendanceRecords)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Scope, (string?)null)));
        using var client = await session.Factory.SignedInAsync("bob");

        Assert.Empty(await PeopleAsync(client));
    }

    private static Guid[] Sorted(params Guid[] ids) => [.. ids.Order()];

    private static Task SetScopeAsync(RecordsSession session, string scope) => session.WithDbAsync(db => db.Set<RoleGroupFeature>()
        .Where(x => x.GroupId == "test-records-" + DataScopes.Unit).ExecuteUpdateAsync(x => x.SetProperty(y => y.Scope, scope)));
}
