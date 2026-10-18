using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Work.Chat.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Chat;

public sealed class RunEventRetentionWorkerTests
{
    [Fact]
    public async Task CleanupDeletesEveryExpiredEventOfFinishedRunsInBatchesAndKeepsActiveRunsAndRecentEvents()
    {
        await using var factory = new AppFactory();
        using var client = await factory.SignedInAsync();
        var finished = await CreateRun(client, (await CreateConversation(client)).Id, "已結束");
        await WaitForTerminal(client, finished.Id);
        var active = await CreateRun(client, (await CreateConversation(client)).Id, "進行中");
        await WaitForTerminal(client, active.Id);
        var expired = DateTimeOffset.UtcNow - RunEventRetentionWorker.Retention - TimeSpan.FromHours(1);
        // 資料超過兩批，因此清理必須重複執行，直到積壓的資料清空。
        var backlog = RunEventRetentionWorker.BatchSize * 2 + 7;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Runs.Where(x => x.Id == active.Id).Select(x => x.OwnerId).SingleAsync();
            await db.Runs.Where(x => x.Id == active.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.ActiveOwnerId, owner));
            await db.RunEvents.Where(x => x.RunId == finished.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.CreatedAt, DateTimeOffset.UtcNow));
            db.RunEvents.AddRange(Enumerable.Range(1, backlog).Select(i => new RunEvent { RunId = finished.Id, Sequence = 1000 + i, CreatedAt = expired }));
            db.RunEvents.AddRange(Enumerable.Range(1, 5).Select(i => new RunEvent { RunId = active.Id, Sequence = 1000 + i, CreatedAt = expired }));
            await db.SaveChangesAsync();
        }
        var recent = await CountAsync(factory, finished.Id, x => x.Sequence < 1000);
        var activeEvents = await CountAsync(factory, active.Id, _ => true);

        var deleted = await ActivatorUtilities.CreateInstance<RunEventRetentionWorker>(factory.Services).CleanupAsync(CancellationToken.None);

        Assert.Equal(backlog, deleted);
        Assert.Equal(0, await CountAsync(factory, finished.Id, x => x.Sequence > 1000));
        Assert.Equal(recent, await CountAsync(factory, finished.Id, _ => true));
        Assert.Equal(activeEvents, await CountAsync(factory, active.Id, _ => true));
    }

    private static async Task<int> CountAsync(AppFactory factory, Guid run, System.Linq.Expressions.Expression<Func<RunEvent, bool>> where)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().RunEvents.Where(x => x.RunId == run).Where(where).CountAsync();
    }
}
