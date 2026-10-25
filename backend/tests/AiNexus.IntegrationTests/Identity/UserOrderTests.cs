using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Identity.Ordering;
using AiNexus.Features.Admin.Operations;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class UserOrderTests
{
    private const string Users = "/api/v1/admin/users";

    [Fact]
    public async Task MovingAUserSortsTheListAndKeepsTheRestByName()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var bob = await OrganizationApi.UserIdAsync(factory, "bob");
        var dave = await OrganizationApi.UserIdAsync(factory, "dave");

        (await MoveAsync(admin, dave, 1)).EnsureSuccessStatusCode();
        (await MoveAsync(admin, bob, 2)).EnsureSuccessStatusCode();

        var list = await ListAsync(admin);
        Assert.Equal(2, list.Sorted);
        Assert.Equal([dave, bob], list.Users.Take(2).Select(x => x.Id));
        Assert.Equal([(int?)10, 20], list.Users.Take(2).Select(x => x.SortOrder));
        Assert.Equal(Enumerable.Range(1, list.Users.Count).Select(x => (int?)x), list.Users.Select(x => x.Position));
        // 未排序的人接在後面，依姓名。
        var rest = list.Users.Skip(2).ToArray();
        Assert.All(rest, x => Assert.Null(x.SortOrder));
        Assert.Equal(rest.Select(x => x.DisplayName).Order(StringComparer.Ordinal), rest.Select(x => x.DisplayName));
        var audits = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.user_order&result=saved"))!;
        Assert.Equal(2, audits.Length);
        Assert.Contains(audits, x => x.ResourceId == dave && x.DetailsJson!.Contains("\"sortOrder\":10"));
    }

    [Fact]
    public async Task SearchKeepsThePositionInTheWholeList()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var bob = await OrganizationApi.UserIdAsync(factory, "bob");
        await OrganizationApi.UserIdAsync(factory, "carol");
        var all = await ListAsync(admin);
        (await MoveAsync(admin, bob, all.Users.Count)).EnsureSuccessStatusCode();

        var found = Assert.Single((await ListAsync(admin, "bob")).Users);
        Assert.Equal(all.Users.Count, found.Position);
        // 超過人數視為最後一位；原地放下不寫入，也不留稽核。
        (await MoveAsync(admin, bob, all.Users.Count + 5)).EnsureSuccessStatusCode();
        Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.user_order&result=saved"))!);
    }

    [Fact]
    public async Task ResetClearsEveryonesOrder()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var bob = await OrganizationApi.UserIdAsync(factory, "bob");
        (await MoveAsync(admin, bob, 1)).EnsureSuccessStatusCode();

        (await admin.DeleteAsync($"{Users}/order")).EnsureSuccessStatusCode();

        var list = await ListAsync(admin);
        Assert.Equal(0, list.Sorted);
        Assert.All(list.Users, x => Assert.Null(x.SortOrder));
        var audit = Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.user_order_reset&result=saved"))!);
        Assert.Contains(bob.ToString(), audit.DetailsJson!);
    }

    [Fact]
    public async Task DeletedOrUnknownUsersAndBadPositionsAreRejected()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var bob = await OrganizationApi.UserIdAsync(factory, "bob");
        var carol = await OrganizationApi.UserIdAsync(factory, "carol");
        (await admin.DeleteAsync($"{Users}/{carol}")).EnsureSuccessStatusCode();

        await AssertCodeAsync(await MoveAsync(admin, carol, 1), HttpStatusCode.Conflict, "user_order_stale");
        await AssertCodeAsync(await MoveAsync(admin, Guid.NewGuid(), 1), HttpStatusCode.Conflict, "user_order_stale");
        await AssertCodeAsync(await MoveAsync(admin, bob, 0), HttpStatusCode.BadRequest, "invalid_user_order");
        Assert.Equal(0, (await ListAsync(admin)).Sorted);
    }

    [Fact]
    public async Task MembersCannotChangeTheOrder()
    {
        await using var factory = new AppFactory(); using var member = await factory.SignedInAsync();
        await AssertCodeAsync(await MoveAsync(member, Guid.NewGuid(), 1), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.DeleteAsync($"{Users}/order"), HttpStatusCode.Forbidden, "feature_forbidden");
    }

    private static Task<HttpResponseMessage> MoveAsync(HttpClient client, Guid id, int position)
        => client.PutAsJsonAsync($"{Users}/{id}/position", new UserPositionRequest(position));

    private static async Task<AdminUsersDto> ListAsync(HttpClient client, string? search = null)
        => (await client.GetFromJsonAsync<AdminUsersDto>(search is null ? Users : $"{Users}?search={search}"))!;
}
