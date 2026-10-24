using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Collaboration;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.ProjectApi;

namespace AiNexus.IntegrationTests.Collaboration;

public sealed class SearchDirectoryTests
{
    [Fact]
    public async Task DisabledPeopleCannotBeFoundOrNewlyAddedButExistingMembershipsStay()
    {
        await using var f = new AppFactory();
        using var alice = await f.SignedInAsync();
        using var carol = await f.SignedInAsync("carol");
        using var carl = await f.SignedInAsync("carl");
        var carolId = (await carol.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var carlId = (await carl.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var project = await CreateProject(alice, "共用專案");
        var access = $"/api/v1/projects/{project.Resource.Id}/access";
        await Share(alice, access, (carolId, "viewer"));

        await DisableAsync(f, carolId);
        await DisableAsync(f, carlId);
        Assert.Empty((await alice.GetFromJsonAsync<DirectoryUserDto[]>("/api/v1/directory?search=car"))!);

        // 已是成員的停用者可以留在清單中一起儲存（停用只凍結）；新加入停用者則拒絕。
        await Share(alice, access, (carolId, "editor"));
        Assert.Equal("editor", (await alice.GetFromJsonAsync<ResourceAclDto>(access))!.Members.Single().Role);
        await AssertCodeAsync(await alice.PutAsJsonAsync(access, new ResourceAclRequest([new(carolId, "editor"), new(carlId, "viewer")], [])),
            HttpStatusCode.BadRequest, "unknown_resource_member");
    }
}
