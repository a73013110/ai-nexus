using AiNexus.Data.App;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.AttachmentApi;
using static AiNexus.IntegrationTests.Support.ProjectApi;

namespace AiNexus.IntegrationTests.Collaboration;

public sealed class ContainerLockTests
{
    // 工作還沒提交時，RCSI 的檢查看不到它：刪除要等它提交才能下判斷。
    [Fact]
    public async Task DeletingAProjectWaitsForWorkBeingStartedAndThenRefuses()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        using var owner = await factory.SignedInAsync();
        var project = (await CreateProject(owner, "container")).Resource.Id;
        var actor = (await owner.GetFromJsonAsync<MeDto>("/api/v1/me", ct))!.Id;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Assert.True(await scope.ServiceProvider.GetRequiredService<ContainerLock>().TryStartWorkAsync(project, ct));
        scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(actor, project, Guid.NewGuid(), "test-work", "進行中的工作");
        await db.SaveChangesAsync(ct);

        var delete = owner.DeleteAsync($"/api/v1/projects/{project}", ct);
        await Task.Delay(200, ct);
        Assert.False(delete.IsCompleted);

        await transaction.CommitAsync(ct);
        var refused = await delete;
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("resource_tasks_active", await refused.Content.ReadAsStringAsync(ct));
    }

    // 對方的存活檢查要在拿到鎖之後才做，否則刪除一提交，它就會把工作寫進已刪除的專案。
    [Fact]
    public async Task WorkStartedDuringADeletionWaitsAndSeesTheProjectGone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        using var owner = await factory.SignedInAsync();
        var project = (await CreateProject(owner, "container")).Resource.Id;
        var file = await Upload(owner, "note.txt", "專案參考");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        Assert.True(await scope.ServiceProvider.GetRequiredService<ContainerLock>().TryDeleteAsync(project, ct));
        await db.Resources.Where(x => x.Id == project).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true), ct);

        var add = owner.PostAsJsonAsync($"/api/v1/projects/{project}/files", new AddDocumentRequest(file.Id), ct);
        await Task.Delay(200, ct);
        Assert.False(add.IsCompleted);

        await transaction.CommitAsync(ct);
        Assert.Equal(HttpStatusCode.NotFound, (await add).StatusCode);
    }

    [Fact]
    public async Task WorkInOneProjectDoesNotWaitForOtherWorkInIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AppFactory();
        using var owner = await factory.SignedInAsync();
        var project = (await CreateProject(owner, "container")).Resource.Id;

        await using var scope = factory.Services.CreateAsyncScope();
        await using var transaction = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.BeginTransactionAsync(ct);
        Assert.True(await scope.ServiceProvider.GetRequiredService<ContainerLock>().TryStartWorkAsync(project, ct));

        Assert.NotEqual(Guid.Empty, (await AddProjectFile(owner, project, "同時加入.txt").WaitAsync(TimeSpan.FromSeconds(30), ct)).Id);
    }
}
