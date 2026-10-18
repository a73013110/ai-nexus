using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Artifacts;
using AiNexus.Features.Work.Sharing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AiNexus.Features.Work.Artifacts.Management;
using AiNexus.Features.Work.Sharing.Management;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Sharing;

public sealed class ListSharesTests
{
    [Fact]
    public async Task ShareListsKeepSenderRecipientAndMissingSourceRules()
    {
        await using var factory = new AppFactory();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        using var carol = await factory.SignedInAsync("carol");
        var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var carolId = (await carol.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var conversation = await CreateConversation(alice);
        using var created = await alice.PostAsJsonAsync("/api/v1/artifacts", new CreateArtifactRequest("成果", "內容"));
        created.EnsureSuccessStatusCode();
        var artifact = (await created.Content.ReadFromJsonAsync<ArtifactDto>())!;
        async Task<ShareDto> CreateShare(CreateShareRequest request)
        {
            using var response = await alice.PostAsJsonAsync("/api/v1/shares", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ShareDto>())!;
        }
        var chat = await CreateShare(new("conversation", conversation.Id, [bobId, carolId]));
        var document = await CreateShare(new("artifact", artifact.Resource.Id, [bobId]));
        (await carol.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", (await CreateConversation(carol)).Id, [bobId]))).EnsureSuccessStatusCode();

        var sent = (await alice.GetFromJsonAsync<ShareDto[]>("/api/v1/shares?sent=true"))!;
        Assert.Equal([document.Id, chat.Id], sent.Select(x => x.Id));
        Assert.All(sent, x => { Assert.True(x.IsOwner); Assert.Equal("alice", x.Owner); });
        Assert.Equal(["bob", "carol"], sent.Single(x => x.Id == chat.Id).Recipients.Order());
        Assert.Equal(["bob"], sent.Single(x => x.Id == document.Id).Recipients);
        var received = (await bob.GetFromJsonAsync<ShareDto[]>("/api/v1/shares"))!;
        Assert.Equal(3, received.Length);
        Assert.All(received, x => { Assert.False(x.IsOwner); Assert.Empty(x.Recipients); });
        Assert.Equal("carol", received[0].Owner); Assert.Equal(["alice", "alice"], received.Skip(1).Select(x => x.Owner));
        Assert.Empty((await alice.GetFromJsonAsync<ShareDto[]>("/api/v1/shares"))!);

        // 來源已消失但未撤銷分享時，對接收者隱藏，但仍會列在分享者的清單中。
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Resource>().Where(x => x.Id == artifact.Resource.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true));
        Assert.DoesNotContain((await bob.GetFromJsonAsync<ShareDto[]>("/api/v1/shares"))!, x => x.Id == document.Id);
        Assert.Contains((await alice.GetFromJsonAsync<ShareDto[]>("/api/v1/shares?sent=true"))!, x => x.Id == document.Id);
        Assert.Single((await carol.GetFromJsonAsync<ShareDto[]>("/api/v1/shares"))!);
    }
}
