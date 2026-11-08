using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Indexing;
using AiNexus.Platform.Data;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

/// <summary>
/// 把等待重建索引的文件（<c>Status = reindex</c>，由維運以 SQL 標記）排進背景工作；設定換了 embedding 模型、目標設定檔還在 building 時，
/// 若開啟 <c>AutoActivate</c> 就以第一位管理員的名義排一個重建工作。已有進行中的工作時不重複排。
/// </summary>
internal sealed class EmbeddingQueueTask(AppDbContext db, EmbeddingProfiles profiles, JobService jobs, IOptions<KnowledgeOptions> options) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "knowledge.embedding-queue", "索引排入", "知識庫", "把等待重建索引的文件排進背景工作；換模型時視設定排入整批重建。", Schedule.Every(TimeSpan.FromMinutes(1)));

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var target = await profiles.TargetAsync(ct);
        var pending = await (from d in db.Documents
                             join r in db.Resources.IgnoreQueryFilters([SoftDelete.Filter]) on d.Id equals r.Id
                             where !d.IsDeleted && d.CollectionId != null && d.Status == "reindex" && !db.BackgroundJobs.Any(j => j.SubjectId == d.Id && j.ActiveKey != null)
                             select new { Document = d, r.OwnerId }).ToListAsync(ct);
        foreach (var item in pending)
        {
            item.Document.Status = "indexing";
            item.Document.JobId = jobs.Enqueue(item.OwnerId, item.Document.Id, item.Document.Id, DocumentEmbeddingHandler.JobKind, item.Document.FileName).Id;
        }

        var rebuild = false;
        if (options.Value.Embedding.AutoActivate && target.Status == "building")
        {
            var actor = await db.UserRoles.Where(x => x.RoleId == BuiltInAccess.AdministratorRole).Select(x => (Guid?)x.UserId).FirstOrDefaultAsync(ct);
            var subject = EmbeddingJobs.Subject(target.Id);
            if (actor is Guid owner && !await db.BackgroundJobs.AnyAsync(x => x.Kind == EmbeddingReindexHandler.JobKind && x.SubjectId == subject, ct))
            {
                jobs.Enqueue(owner, null, subject, EmbeddingReindexHandler.JobKind, $"重建 {target.Model} 檢索索引");
                rebuild = true;
            }
        }
        await db.SaveChangesAsync(ct);
        if (pending.Count == 0 && !rebuild) return TaskOutcome.Idle();
        return TaskOutcome.Done(rebuild ? $"排入 {pending.Count:N0} 份文件與 {target.Model} 整批重建" : $"排入 {pending.Count:N0} 份文件");
    }
}
