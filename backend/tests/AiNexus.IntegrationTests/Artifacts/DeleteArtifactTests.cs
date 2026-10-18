using AiNexus.Data.App;
using AiNexus.Data.App.Sharing;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Artifacts;
using AiNexus.Features.Work.Artifacts.Management;
using AiNexus.Features.Work.Sharing;
using AiNexus.Features.Work.Sharing.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Artifacts;

public sealed class DeleteArtifactTests
{
    [Fact]
    public async Task DeletingAnArtifactRevokesItsSharesInTheSameTransaction()
    {
        await using var f = new AppFactory(); using var alice = await f.SignedInAsync(); using var bob = await f.SignedInAsync("bob");
        var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var source = (await (await alice.PostAsJsonAsync("/api/v1/artifacts", new CreateArtifactRequest("成果", "內容"))).Content.ReadFromJsonAsync<ArtifactDto>())!;
        var share = (await (await alice.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("artifact", source.Resource.Id, [bobId]))).Content.ReadFromJsonAsync<ShareDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/artifacts/{source.Resource.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync($"/api/v1/shares/{share.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/v1/artifacts/{source.Resource.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/shares/{share.Id}")).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = await db.Set<ShareLink>().SingleAsync(x => x.Id == share.Id);
        Assert.True(link.IsRevoked); Assert.Equal("", link.SnapshotJson);
        Assert.Single(await db.AuditEvents.Where(x => x.ResourceId == source.Resource.Id && x.Action == "artifact.deleted").ToArrayAsync());
    }
}
