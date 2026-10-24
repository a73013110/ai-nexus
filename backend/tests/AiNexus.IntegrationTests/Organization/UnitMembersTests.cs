using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Organization;
using AiNexus.Features.Admin.Organization.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Organization;

public sealed class UnitMembersTests
{
    [Fact]
    public async Task PrimaryMembershipMovesBetweenUnitsInOneChange()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var bob = await UserIdAsync(factory, "bob");
        var company = await CreateUnitAsync(admin, "總公司");
        var it = await CreateUnitAsync(admin, "資訊處", company.Id);
        var hr = await CreateUnitAsync(admin, "人資處", company.Id);

        (await SaveMemberAsync(admin, it.Id, bob, primary: true)).EnsureSuccessStatusCode();
        (await SaveMemberAsync(admin, hr.Id, bob, manager: true)).EnsureSuccessStatusCode();
        (await SaveMemberAsync(admin, hr.Id, bob, primary: true, manager: true)).EnsureSuccessStatusCode();

        var member = Assert.Single(await MembersAsync(admin, hr.Id));
        Assert.Equal((bob, true, true, true), (member.UserId, member.Active, member.IsPrimary, member.IsManager));
        Assert.False(Assert.Single(await MembersAsync(admin, it.Id)).IsPrimary);
        Assert.Equal(1, (await ListUnitsAsync(admin)).Single(x => x.Id == it.Id).MemberCount);

        var units = (await admin.GetFromJsonAsync<UserUnitDto[]>($"/api/v1/admin/users/{bob}/organization-units"))!;
        Assert.Equal([hr.Id, it.Id], units.Select(x => x.UnitId));
        Assert.Equal(["總公司", "人資處"], units[0].Path);
        // 稽核快照是該使用者的全部所屬，看得出主要所屬從哪裡移走。
        var audit = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.organization_member&result=saved"))!;
        Assert.Equal(3, audit.Length);
        Assert.Contains("memberships", audit[0].DetailsJson);
    }

    [Fact]
    public async Task OnlyExistingUsersJoinAndDisabledUnitsTakeNoNewMembers()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var bob = await UserIdAsync(factory, "bob");
        var carol = await UserIdAsync(factory, "carol");
        var closed = await CreateUnitAsync(admin, "已裁撤", enabled: false);
        var open = await CreateUnitAsync(admin, "營運處");

        await AssertCodeAsync(await SaveMemberAsync(admin, open.Id, Guid.NewGuid()), HttpStatusCode.NotFound, "organization_user_not_found");
        await AssertCodeAsync(await SaveMemberAsync(admin, Guid.NewGuid(), bob), HttpStatusCode.NotFound, "organization_unit_not_found");
        await AssertCodeAsync(await SaveMemberAsync(admin, closed.Id, bob), HttpStatusCode.Conflict, "organization_unit_disabled");

        (await SaveMemberAsync(admin, open.Id, bob)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"{Units}/{open.Id}", new UnitUpdateRequest(null, "營運處", false))).EnsureSuccessStatusCode();
        // 停用後既有成員仍可修改。
        (await SaveMemberAsync(admin, open.Id, bob, manager: true)).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(x => x.Id == carol).ExecuteUpdateAsync(x => x.SetProperty(y => y.DeletedAt, DateTimeOffset.UnixEpoch));
        }
        (await admin.PutAsJsonAsync($"{Units}/{open.Id}", new UnitUpdateRequest(null, "營運處", true))).EnsureSuccessStatusCode();
        await AssertCodeAsync(await SaveMemberAsync(admin, open.Id, carol), HttpStatusCode.NotFound, "organization_user_not_found");
        await AssertCodeAsync(await admin.DeleteAsync($"{Units}/{open.Id}/members/{carol}"), HttpStatusCode.NotFound, "organization_member_not_found");
        await AssertCodeAsync(await admin.GetAsync($"{Units}/{Guid.NewGuid()}/members"), HttpStatusCode.NotFound, "organization_unit_not_found");
    }

    [Fact]
    public async Task CandidatesAreActiveUsersWithTheirPrimaryUnit()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var alice = await UserIdAsync(factory, "alice");
        var bob = await UserIdAsync(factory, "bob");
        var carol = await UserIdAsync(factory, "carol");
        var dave = await UserIdAsync(factory, "dave");
        var it = await CreateUnitAsync(admin, "資訊處");
        var hr = await CreateUnitAsync(admin, "人資處");
        (await SaveMemberAsync(admin, it.Id, bob, primary: true)).EnsureSuccessStatusCode();
        (await SaveMemberAsync(admin, hr.Id, bob)).EnsureSuccessStatusCode();
        (await SaveMemberAsync(admin, hr.Id, carol)).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(x => x.Id == dave).ExecuteUpdateAsync(x => x.SetProperty(y => y.Enabled, false));
        }

        var candidates = (await admin.GetFromJsonAsync<UnitCandidateDto[]>($"{Units}/candidates"))!;
        // 停用＝離職，不列出；沒有主要所屬的人 PrimaryUnitId 為 null。
        Assert.Equal([(alice, null), (bob, it.Id), (carol, null)], candidates.Select(x => (x.Id, x.PrimaryUnitId)));
        using var member = await factory.SignedInAsync("erin");
        await AssertCodeAsync(await member.GetAsync($"{Units}/candidates"), HttpStatusCode.Forbidden, "feature_forbidden");
    }

    [Fact]
    public async Task MembersSeeNeitherUnitsNorOtherUsersMemberships()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var alice = await UserIdAsync(factory, "alice");
        using var member = await factory.SignedInAsync("bob");
        await AssertCodeAsync(await member.GetAsync($"/api/v1/admin/users/{alice}/organization-units"), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.PutAsJsonAsync($"{Units}/{Guid.NewGuid()}/members/{alice}", new UnitMemberRequest(true, false)), HttpStatusCode.Forbidden, "feature_forbidden");
        Assert.Empty((await admin.GetFromJsonAsync<UserUnitDto[]>($"/api/v1/admin/users/{alice}/organization-units"))!);
    }
}
