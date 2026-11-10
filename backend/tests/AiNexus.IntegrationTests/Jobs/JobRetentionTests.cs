using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Features.Work.Jobs.Retention;
using AiNexus.Features.Work.Knowledge.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Jobs;

public sealed class JobRetentionTests
{
    [Fact]
    public async Task OnlyFinishedJobsPastRetentionThatNothingRefersToAreDeleted()
    {
        await using var f = new AppFactory();
        using var client = await f.SignedInAsync();
        var (_, owner, document) = await KnowledgeApi.SeedAsync(f, client);
        var old = DateTimeOffset.UtcNow.AddDays(-200);
        Guid current, expired, running, recent, retried;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // 文件處理留下的工作全部變舊：只有文件目前指著的那一件要留下。
            await db.BackgroundJobs.ExecuteUpdateAsync(p => p.SetProperty(x => x.CreatedAt, old).SetProperty(x => x.UpdatedAt, old));
            current = (await db.Documents.Where(x => x.Id == document.Id).Select(x => x.JobId).SingleAsync())!.Value;
            expired = Add(db, owner, RunStates.Failed, old, old);
            running = Add(db, owner, RunStates.Running, old, old);
            recent = Add(db, owner, RunStates.Completed, DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-10));
            // 很久以前建立、最近才重試結束：保存期從最後一次更新起算。
            retried = Add(db, owner, RunStates.Failed, old, DateTimeOffset.UtcNow.AddDays(-1));
            await db.SaveChangesAsync();
        }

        var deleted = await f.InScopeAsync((JobRetentionTask task) => task.DeleteExpiredAsync(CancellationToken.None));

        Assert.True(deleted >= 1);
        using (var scope = f.Services.CreateScope())
        {
            var left = await scope.ServiceProvider.GetRequiredService<AppDbContext>().BackgroundJobs.Select(x => x.Id).ToListAsync();
            Assert.Equal(new[] { current, running, recent, retried }.Order(), left.Order());
            Assert.DoesNotContain(expired, left);
        }
        // 文件仍查得到它的處理工作。
        Assert.Equal(current, (await client.GetFromJsonAsync<DocumentJobDto>($"/api/v1/documents/{document.Id}/job"))!.Job.Id);
        Assert.Equal(0, await f.InScopeAsync((JobRetentionTask task) => task.DeleteExpiredAsync(CancellationToken.None)));
    }

    /// <summary>
    /// 有外鍵指向工作的資料表都要有 <see cref="IJobReferences"/> 宣告：漏掉的話，刪除會被外鍵擋下，整個清理任務每一輪都失敗。
    /// 以各來源查詢的 SQL 是否讀到那張表來判斷。
    /// </summary>
    [Fact]
    public async Task EveryTableWithAForeignKeyToJobsDeclaresItsReferences()
    {
        await using var f = new AppFactory();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var declared = string.Join("\n", scope.ServiceProvider.GetServices<IJobReferences>().Select(x => x.JobIds(db).ToQueryString()));

        var tables = db.Model.FindEntityType(typeof(BackgroundJob))!.GetReferencingForeignKeys()
            .Select(x => $"[{x.DeclaringEntityType.GetSchema()}].[{x.DeclaringEntityType.GetTableName()}]").Distinct().ToList();

        Assert.NotEmpty(tables);
        Assert.All(tables, table => Assert.Contains(table, declared));
        // 沒有外鍵、靠宣告才留得住的引用。
        Assert.Contains("[knowledge].[Documents]", declared);
    }

    private static Guid Add(AppDbContext db, Guid owner, string status, DateTimeOffset created, DateTimeOffset updated)
    {
        var job = new BackgroundJob
        {
            OwnerId = owner, OperationId = Guid.NewGuid(), SubjectId = Guid.NewGuid(), Kind = "fixture", Label = "保存期測試",
            Status = status, CreatedAt = created, UpdatedAt = updated,
            LeaseToken = status == RunStates.Running ? Guid.NewGuid() : null,
            LeaseUntil = status == RunStates.Running ? DateTimeOffset.UtcNow.AddHours(1) : null,
        };
        db.Add(job);
        return job.Id;
    }
}
