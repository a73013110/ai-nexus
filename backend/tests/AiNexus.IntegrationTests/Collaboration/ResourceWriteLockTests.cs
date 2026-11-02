using AiNexus.Data.App;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Artifacts.Management;
using AiNexus.Features.Work.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ArtifactApi;
using static AiNexus.IntegrationTests.Support.ProjectApi;

namespace AiNexus.IntegrationTests.Collaboration;

public sealed class ResourceWriteLockTests
{
    // 每個請求各自一條連線，等於不同程序同時改：ACL 是先讀舊列再比對，不排隊就會同時新增同一位成員而撞上主鍵。
    [Fact]
    public async Task ConcurrentAclChangesOnOneResourceAllSucceed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        using var owner = await factory.SignedInAsync();
        using var member = await factory.SignedInAsync("member");
        var project = (await CreateProject(owner, "acl")).Resource.Id;
        var memberId = (await member.GetFromJsonAsync<MeDto>("/api/v1/me", ct))!.Id;

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            owner.PutAsJsonAsync($"/api/v1/projects/{project}/access", new ResourceAclRequest([new(memberId, "editor")], []), ct)));

        Assert.All(responses, x => Assert.True(x.IsSuccessStatusCode));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().ResourceMembers.CountAsync(x => x.ResourceId == project, ct));
    }

    [Fact]
    public async Task AWriteWaitsOnlyForItsOwnResource()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        using var owner = await factory.SignedInAsync();
        var busy = (await CreateProject(owner, "busy")).Resource.Id;
        var other = (await CreateProject(owner, "other")).Resource.Id;

        await using var scope = factory.Services.CreateAsyncScope();
        await using var transaction = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.BeginTransactionAsync(ct);
        Assert.True(await scope.ServiceProvider.GetRequiredService<ResourceWriteLock>().TryAcquireAsync(busy, ct));

        (await owner.PutAsJsonAsync($"/api/v1/projects/{other}/access", new ResourceAclRequest([], []), ct).WaitAsync(TimeSpan.FromSeconds(30), ct)).EnsureSuccessStatusCode();
        var waiting = owner.PutAsJsonAsync($"/api/v1/projects/{busy}/access", new ResourceAclRequest([], []), ct);
        await Task.Delay(200, ct);
        Assert.False(waiting.IsCompleted);

        await transaction.CommitAsync(ct);
        (await waiting).EnsureSuccessStatusCode();
    }

    // 存活檢查要在拿到鎖之後才做，否則刪除一提交，版本就會寫進已刪除的成果。
    [Fact]
    public async Task SavingAnArtifactDuringItsDeletionWaitsAndSeesItGone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        using var owner = await factory.SignedInAsync();
        var artifact = await CreateArtifact(owner);
        var id = artifact.Resource.Id;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Assert.True(await scope.ServiceProvider.GetRequiredService<ResourceWriteLock>().TryAcquireAsync(id, ct));
        await db.Resources.Where(x => x.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true), ct);

        var save = owner.PutAsJsonAsync($"/api/v1/artifacts/{id}", new SaveArtifactRequest("工作成果", "新內容", artifact.CurrentVersion), ct);
        await Task.Delay(200, ct);
        Assert.False(save.IsCompleted);

        await transaction.CommitAsync(ct);
        Assert.Equal(HttpStatusCode.NotFound, (await save).StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        Assert.Equal(artifact.CurrentVersion, await check.ServiceProvider.GetRequiredService<AppDbContext>().ArtifactRevisions.CountAsync(x => x.ArtifactId == id, ct));
    }
}
