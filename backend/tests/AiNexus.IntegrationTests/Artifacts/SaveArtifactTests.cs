using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Collaboration;
using static AiNexus.IntegrationTests.Support.ArtifactApi;

namespace AiNexus.IntegrationTests.Artifacts;

public sealed class SaveArtifactTests
{
    [Fact]
    public async Task ImmutableVersionsConflictRatherThanOverwritingAndReadonlyMembersCannotEdit()
    {
        await using var factory = new NexusFactory(); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var artifact = await CreateArtifact(alice); var id = artifact.Resource.Id;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/artifacts/{id}")).StatusCode);
        (await alice.PutAsJsonAsync($"/api/v1/artifacts/{id}", new SaveArtifactRequest("修改後標題", "新版本內容", 1))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PutAsJsonAsync($"/api/v1/artifacts/{id}", new SaveArtifactRequest("晚到編輯", "不應覆蓋", 1))).StatusCode);
        var original = (await alice.GetFromJsonAsync<ArtifactDto>($"/api/v1/artifacts/{id}?version=1"))!; Assert.Equal(Content, original.Content); Assert.Equal(2, original.CurrentVersion); Assert.Equal("工作成果", original.Resource.Name);
        var user = (await bob.GetFromJsonAsync<AiNexus.Features.Account.MeDto>("/api/v1/me"))!;
        (await alice.PutAsJsonAsync($"/api/v1/artifacts/{id}/access", new ResourceAclRequest([new(user.Id, "viewer")], []))).EnsureSuccessStatusCode();
        Assert.Equal("新版本內容", (await bob.GetFromJsonAsync<ArtifactDto>($"/api/v1/artifacts/{id}"))!.Content);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PutAsJsonAsync($"/api/v1/artifacts/{id}", new SaveArtifactRequest("讀者編輯", "應拒絕", 2))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/artifacts/{id}")).StatusCode);
        (await alice.DeleteAsync($"/api/v1/artifacts/{id}")).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/artifacts/{id}?version=1")).StatusCode);
    }
}
