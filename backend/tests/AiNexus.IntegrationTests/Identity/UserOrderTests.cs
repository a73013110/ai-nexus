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
        var audits = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=identity.user.moved&result=success"))!;
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
        // 超過人數視為最後一位；原地放下不寫入：請求照樣留稽核，但沒有前後快照。
        (await MoveAsync(admin, bob, all.Users.Count + 5)).EnsureSuccessStatusCode();
        var moves = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=identity.user.moved&result=success"))!;
        Assert.Equal(2, moves.Length); Assert.DoesNotContain("\"before\"", moves[0].DetailsJson ?? "");
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
        var audit = Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=identity.user.reordered&result=success"))!);
        Assert.Contains(bob.ToString(), audit.DetailsJson!);
    }

    [Fact]
    public async Task NewUsersJoinTheEndOfTheSortedUsersOnceThereIsAnOrder()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        // 還沒有人排序：新人維持未排序，不會變成唯一已排序的人而排到最前面。
        var first = await IdentityApi.CreateUser(admin, IdentityApi.LocalAccount("first"));
        Assert.Null((await ListAsync(admin)).Users.Single(x => x.Id == first).SortOrder);

        var bob = await OrganizationApi.UserIdAsync(factory, "bob");
        (await MoveAsync(admin, bob, 1)).EnsureSuccessStatusCode();
        var second = await IdentityApi.CreateUser(admin, IdentityApi.LocalAccount("second"));

        var list = await ListAsync(admin);
        Assert.Equal(2, list.Sorted);
        Assert.Equal([bob, second], list.Users.Take(2).Select(x => x.Id));
        Assert.Equal([(int?)10, 20], list.Users.Take(2).Select(x => x.SortOrder));
        Assert.Null(list.Users.Single(x => x.Id == first).SortOrder);
    }

    [Fact]
    public async Task DirectoryImportAndFirstSignInAlsoJoinTheEnd()
    {
        await using var factory = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = factory.CreateClient();
        var alice = (await IdentityApi.Login(admin, "alice")).Id;
        // 還沒有人排序時匯入的人維持未排序。
        var carol = Assert.Single(await DirectoryApi.Import(admin, FixtureAdDirectory.Carol)).UserId!.Value;
        Assert.Equal(0, (await ListAsync(admin)).Sorted);
        (await MoveAsync(admin, carol, 1)).EnsureSuccessStatusCode();

        using var bobClient = factory.CreateClient();
        var bob = (await IdentityApi.Login(bobClient, "bob")).Id;
        var erin = Assert.Single(await DirectoryApi.Import(admin, FixtureAdDirectory.Erin)).UserId!.Value;

        var list = await ListAsync(admin);
        Assert.Equal([carol, bob, erin, alice], list.Users.Select(x => x.Id));
        Assert.Equal([(int?)10, 20, 30, null], list.Users.Select(x => x.SortOrder));
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
