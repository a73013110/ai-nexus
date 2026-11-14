using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.OrganizationApi;

namespace AiNexus.IntegrationTests.Organization;

public sealed class OrganizationDirectoryTests
{
    [Fact]
    public async Task DisabledUnitsTakeTheirWholeSubtreeOutAndInactiveUsersDoNotCount()
    {
        await using var factory = new AppFactory();
        var bob = await UserIdAsync(factory, "bob");
        var carol = await UserIdAsync(factory, "carol");
        Guid company = Guid.NewGuid(), it = Guid.NewGuid(), systems = Guid.NewGuid(), closed = Guid.NewGuid(), under = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(
            new Unit { Id = company, Name = "總公司", SortOrder = 10 },
            new Unit { Id = it, ParentId = company, Name = "資訊處", SortOrder = 10 },
            new Unit { Id = systems, ParentId = it, Name = "系統部", SortOrder = 10 },
            new Unit { Id = closed, ParentId = company, Name = "已裁撤", Enabled = false, SortOrder = 20 },
            new Unit { Id = under, ParentId = closed, Name = "仍啟用的下層", SortOrder = 10 });
        await db.SaveChangesAsync();
        db.AddRange(
            new UnitMember { UnitId = it, UserId = bob, IsPrimary = true, IsManager = true },
            new UnitMember { UnitId = under, UserId = bob },
            new UnitMember { UnitId = systems, UserId = carol, IsPrimary = true });
        await db.SaveChangesAsync();
        var directory = scope.ServiceProvider.GetRequiredService<OrganizationDirectory>();

        Assert.Equal([new UnitMembership(it, true, true)], await directory.MembershipsAsync(bob, default));
        Assert.Equal(new HashSet<Guid> { company, it, systems }, await directory.SubtreeAsync([company], default));
        Assert.Empty(await directory.SubtreeAsync([under, Guid.NewGuid()], default));
        Assert.Equal([bob], await directory.MemberIds([it]).ToListAsync());

        await db.Users.Where(x => x.Id == carol).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        Assert.Empty(await directory.MembershipsAsync(carol, default));
        Assert.Empty(await directory.MemberIds([systems]).ToListAsync());
    }

    // 簽核「申請人的主管」：沒有主管的單位不成一層；公司由首長單位帶領，總經理在董事長之前，總經理自己往上只到董事長。
    [Fact]
    public async Task ManagerChainSkipsVacantUnitsAndClimbsHeadUnitsByRank()
    {
        await using var factory = new AppFactory();
        Dictionary<string, Guid> users = [];
        foreach (var name in new[] { "bob", "dave", "grace", "frank", "erin" }) users[name] = await UserIdAsync(factory, name);
        Guid company = Guid.NewGuid(), board = Guid.NewGuid(), office = Guid.NewGuid(), engineering = Guid.NewGuid(), systems = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(
            new Unit { Id = company, Name = "總公司", SortOrder = 10 },
            new Unit { Id = board, ParentId = company, Name = "董事會", SortOrder = 10, HeadRank = 1 },
            new Unit { Id = office, ParentId = company, Name = "總經理室", SortOrder = 20, HeadRank = 2 },
            new Unit { Id = engineering, ParentId = company, Name = "工程部", SortOrder = 30 },
            new Unit { Id = systems, ParentId = engineering, Name = "系統組", SortOrder = 10 });
        await db.SaveChangesAsync();
        db.AddRange(
            new UnitMember { UnitId = board, UserId = users["frank"], IsPrimary = true, IsManager = true },
            new UnitMember { UnitId = office, UserId = users["grace"], IsPrimary = true, IsManager = true },
            new UnitMember { UnitId = engineering, UserId = users["dave"], IsPrimary = true, IsManager = true },
            new UnitMember { UnitId = systems, UserId = users["bob"], IsPrimary = true });
        await db.SaveChangesAsync();
        var directory = scope.ServiceProvider.GetRequiredService<OrganizationDirectory>();

        async Task<Guid[][]> ChainAsync(string name) => [.. (await directory.ManagerChainAsync(users[name], default)).Select(x => x.ToArray())];
        Assert.Equal([[users["dave"]], [users["grace"]], [users["frank"]]], await ChainAsync("bob"));
        Assert.Equal([[users["grace"]], [users["frank"]]], await ChainAsync("dave"));
        Assert.Equal([[users["frank"]]], await ChainAsync("grace"));
        Assert.Empty(await ChainAsync("erin"));
    }
}
