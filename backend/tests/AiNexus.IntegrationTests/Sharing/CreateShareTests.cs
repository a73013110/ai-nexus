using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Sharing;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Work.Collaboration;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Artifacts;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Work.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Features.Work.Sharing.Management;
using AiNexus.Features.Work.Sharing.Viewing;
using AiNexus.Features.Work.Artifacts.Management;
using static AiNexus.IntegrationTests.Support.ArtifactApi;
using static AiNexus.IntegrationTests.Support.ChatApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Sharing;

public sealed class CreateShareTests
{
    [Fact]
    public async Task DisabledRecipientsAreRejected()
    {
        await using var f = new AppFactory(); using var owner = await f.SignedInAsync(); using var reader = await f.SignedInAsync("reader");
        var user = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var conversation = await CreateConversation(owner);
        await DisableAsync(f, user);
        await AssertCodeAsync(await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", conversation.Id, [user])), HttpStatusCode.BadRequest, "share_recipient_unknown");
    }

    // 「自己」是發出這次請求的人：分享給自己被拒，另一位使用者分享給同一人仍成功。
    [Fact]
    public async Task SelfRecipientIsTheCurrentRequestUser()
    {
        await using var f = new AppFactory(); using var owner = await f.SignedInAsync(); using var reader = await f.SignedInAsync("reader");
        var ownerId = (await owner.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        await AssertCodeAsync(await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", (await CreateConversation(owner)).Id, [ownerId])), HttpStatusCode.BadRequest, "share_invalid");
        (await reader.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", (await CreateConversation(reader)).Id, [ownerId]))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ParallelSharesCannotPassTheActiveLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var f = new AppFactory(); using var owner = await f.SignedInAsync(); using var reader = await f.SignedInAsync("reader");
        var ownerId = (await owner.GetFromJsonAsync<MeDto>("/api/v1/me", ct))!.Id;
        var readerId = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me", ct))!.Id;
        var conversations = await Task.WhenAll(CreateConversation(owner), CreateConversation(owner));
        await using (var seed = f.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var _ in Enumerable.Range(0, ShareLink.MaxActivePerOwner - 1))
            {
                var resource = new Resource { OwnerId = ownerId, Kind = "share", Name = "既有分享" };
                db.Add(resource);
                db.Add(new ShareLink { Id = resource.Id, OwnerId = ownerId, SourceId = Guid.NewGuid(), Kind = "conversation", Title = "既有分享", CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
            }
            await db.SaveChangesAsync(ct);
        }

        var responses = await Task.WhenAll(conversations.Select(x => owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", x.Id, [readerId]), ct)));

        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    // 上限是每人的規則：別人的分享不用等，也不像全域鎖一樣排在同一條隊伍。
    [Fact]
    public async Task ASharerWaitsOnlyForTheirOwnAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var f = new AppFactory(); using var alice = await f.SignedInAsync(); using var bob = await f.SignedInAsync("bob"); using var reader = await f.SignedInAsync("reader");
        var aliceId = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me", ct))!.Id;
        var readerId = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me", ct))!.Id;
        var aliceConversation = await CreateConversation(alice); var bobConversation = await CreateConversation(bob);

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockUserRowAsync(aliceId, ct);

        (await bob.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", bobConversation.Id, [readerId]), ct).WaitAsync(TimeSpan.FromSeconds(30), ct)).EnsureSuccessStatusCode();
        var waiting = alice.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", aliceConversation.Id, [readerId]), ct);
        await Task.Delay(200, ct);
        Assert.False(waiting.IsCompleted);

        await transaction.CommitAsync(ct);
        (await waiting).EnsureSuccessStatusCode();
    }

    // 刪除成果會撤銷它的分享；排在刪除之後的分享要看得到成果已刪除，不能留下一筆漏撤銷的分享。
    [Fact]
    public async Task SharingAnArtifactDuringItsDeletionWaitsAndFails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var f = new AppFactory(); using var owner = await f.SignedInAsync(); using var reader = await f.SignedInAsync("reader");
        var readerId = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me", ct))!.Id;
        var artifact = (await CreateArtifact(owner)).Resource.Id;

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Assert.True(await scope.ServiceProvider.GetRequiredService<ResourceWriteLock>().TryAcquireAsync(artifact, ct));
        await db.Resources.Where(x => x.Id == artifact).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true), ct);

        var share = owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("artifact", artifact, [readerId]), ct);
        await Task.Delay(200, ct);
        Assert.False(share.IsCompleted);

        await transaction.CommitAsync(ct);
        Assert.Equal(HttpStatusCode.NotFound, (await share).StatusCode);
    }

    [Fact]
    public async Task ParallelRevokesAllSucceed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var f = new AppFactory(); using var owner = await f.SignedInAsync(); using var reader = await f.SignedInAsync("reader");
        var readerId = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me", ct))!.Id;
        var created = await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", (await CreateConversation(owner)).Id, [readerId]), ct);
        var share = (await created.Content.ReadFromJsonAsync<ShareDto>(ct))!.Id;

        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => owner.DeleteAsync($"/api/v1/shares/{share}", ct)));

        Assert.All(responses, x => Assert.True(x.IsSuccessStatusCode));
        await using var scope = f.Services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AppDbContext>().ShareLinks.AsNoTracking().SingleAsync(x => x.Id == share, ct)).IsRevoked);
    }

    [Fact]
    public async Task AttachmentGrantIsExplicitBranchBoundAndRemovedOnRevoke()
    {
        await using var f = new AppFactory(); using var owner = await f.SignedInAsync(); using var reader = await f.SignedInAsync("reader");
        var user = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        using var body = new MultipartFormDataContent(); body.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("測試附件")), "file", "notes.txt");
        var upload = await owner.PostAsync("/api/v1/attachments", body); upload.EnsureSuccessStatusCode(); var file = (await upload.Content.ReadFromJsonAsync<AttachmentDto>())!;
        var conversation = await CreateConversation(owner);
        var response = await PostRun(owner, new(conversation.Id, "test-model", "閱讀附件", null, null, AttachmentIds: [file.Id])); response.EnsureSuccessStatusCode();
        await WaitForTerminal(owner, (await response.Content.ReadFromJsonAsync<RunDto>())!.Id);
        var textOnly = (await (await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", conversation.Id, [user]))).Content.ReadFromJsonAsync<ShareDto>())!;
        var shared = (await reader.GetFromJsonAsync<SharedContentDto>($"/api/v1/shares/{textOnly.Id}"))!;
        Assert.Equal("測試模型", shared.Snapshot.Messages.Single(x => x.Role == "assistant").ModelDisplayName);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/shares/{textOnly.Id}/files/{file.Id}")).StatusCode);
        var withFiles = (await (await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", conversation.Id, [user], IncludeAttachments: true))).Content.ReadFromJsonAsync<ShareDto>())!;
        Assert.Equal("測試附件", await reader.GetStringAsync($"/api/v1/shares/{withFiles.Id}/files/{file.Id}"));
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/attachments/{file.Id}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/shares/{withFiles.Id}/files/{Guid.NewGuid()}")).StatusCode);
        (await owner.DeleteAsync($"/api/v1/shares/{withFiles.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/shares/{withFiles.Id}/files/{file.Id}")).StatusCode);
        using var scope = f.Services.CreateScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().ResourceAttachments.AnyAsync(x => x.ResourceId == withFiles.Id));
    }

    [Fact]
    public async Task AccountBoundArtifactSnapshotsDoNotFollowLaterEditsAndRevokeImmediately()
    {
        await using var f = new AppFactory(); using var alice = await f.SignedInAsync(); using var bob = await f.SignedInAsync("bob"); using var carol = await f.SignedInAsync("carol");
        var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var source = (await (await alice.PostAsJsonAsync("/api/v1/artifacts", new CreateArtifactRequest("成果", "分享當下的內容"))).Content.ReadFromJsonAsync<ArtifactDto>())!;
        var response = await alice.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("artifact", source.Resource.Id, [bobId])); response.EnsureSuccessStatusCode(); var share = (await response.Content.ReadFromJsonAsync<ShareDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await carol.GetAsync($"/api/v1/shares/{share.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/artifacts/{source.Resource.Id}")).StatusCode);
        (await alice.PutAsJsonAsync($"/api/v1/artifacts/{source.Resource.Id}", new SaveArtifactRequest("成果", "之後的私人修改", 1))).EnsureSuccessStatusCode();
        var view = (await bob.GetFromJsonAsync<SharedContentDto>($"/api/v1/shares/{share.Id}"))!; Assert.Equal("分享當下的內容", view.Snapshot.Content); Assert.Equal(1, view.Snapshot.ArtifactVersion); Assert.Empty(view.Share.Recipients);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/shares/{share.Id}")).StatusCode);
        (await alice.DeleteAsync($"/api/v1/shares/{share.Id}")).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/shares/{share.Id}")).StatusCode);
    }

    [Fact]
    public async Task ExpiryAndSourceDeletionInvalidateSharingAndPurgeSnapshotText()
    {
        await using var f = new AppFactory(); using var owner = await f.SignedInAsync(); using var reader = await f.SignedInAsync("reader"); var user = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var conversation = await CreateConversation(owner); var run = await CreateRun(owner, conversation.Id, "測試對話"); await WaitForTerminal(owner, run.Id);
        var share = (await (await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", conversation.Id, [user], 1))).Content.ReadFromJsonAsync<ShareDto>())!;
        Assert.Equal(2, (await reader.GetFromJsonAsync<SharedContentDto>($"/api/v1/shares/{share.Id}"))!.Snapshot.Messages.Count);
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.ShareLinks.Where(x => x.Id == share.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1))); }
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/shares/{share.Id}")).StatusCode);
        var second = (await (await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", conversation.Id, [user]))).Content.ReadFromJsonAsync<ShareDto>())!;
        (await owner.DeleteAsync($"/api/v1/conversations/{conversation.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/shares/{second.Id}")).StatusCode);
        using var verify = f.Services.CreateScope(); var deleted = await verify.ServiceProvider.GetRequiredService<AppDbContext>().ShareLinks.FindAsync(second.Id); Assert.Equal("", deleted!.SnapshotJson); Assert.True(deleted.IsRevoked);
    }
}
