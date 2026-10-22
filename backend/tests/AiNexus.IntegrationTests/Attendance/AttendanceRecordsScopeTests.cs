using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using Microsoft.EntityFrameworkCore;
using static AiNexus.IntegrationTests.Support.RecordsSession;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>出勤紀錄依 <c>attendance.records</c> 的資料範圍決定看得到誰（ADR 0018）：own、unit、unit-tree、all，多群組以 OR 合併。</summary>
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
                new UnitMember { UnitId = it, UserId = bob, IsPrimary = true, IsManager = true },
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

    private static Task SetScopeAsync(Support.RecordsSession session, string scope) => session.WithDbAsync(db => db.Set<RoleGroupFeature>()
        .Where(x => x.GroupId == "test-records-" + DataScopes.Unit).ExecuteUpdateAsync(x => x.SetProperty(y => y.Scope, scope)));
}
