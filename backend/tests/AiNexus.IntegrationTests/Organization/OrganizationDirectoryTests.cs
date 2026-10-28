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
}
