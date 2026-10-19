using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Indexing;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

public sealed partial class EmbeddingBootstrapWorker(IServiceScopeFactory scopes, StorageReadiness readiness, IHostEnvironment environment, ILogger<EmbeddingBootstrapWorker> logger, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing")) return;
        var cleanedAt = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (readiness.Configured)
                {
                    using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var target = await scope.ServiceProvider.GetRequiredService<EmbeddingProfiles>().TargetAsync(stoppingToken);
                    var jobs = scope.ServiceProvider.GetRequiredService<JobService>();
                    var pending = await (from d in db.Set<Document>() join r in db.Set<Resource>().IgnoreQueryFilters([SoftDelete.Filter]) on d.Id equals r.Id
                        where !d.IsDeleted && d.CollectionId != null && d.Status == "reindex" && !db.Set<BackgroundJob>().Any(j => j.SubjectId == d.Id && j.ActiveKey != null)
                        select new { Document = d, r.OwnerId }).ToListAsync(stoppingToken);
                    foreach (var item in pending) { item.Document.Status = "indexing"; item.Document.JobId = jobs.Enqueue(item.OwnerId, item.Document.Id, item.Document.Id, DocumentEmbeddingHandler.JobKind, item.Document.FileName).Id; }
                    var settings = scope.ServiceProvider.GetRequiredService<IOptions<KnowledgeOptions>>().Value;
                    if (settings.Embedding.AutoActivate && target.Status == "building")
                    {
                        var actor = await db.Set<UserRole>().Where(x => x.RoleId == BuiltInAccess.AdministratorRole).Select(x => (Guid?)x.UserId).FirstOrDefaultAsync(stoppingToken);
                        var subject = EmbeddingJobs.Subject(target.Id);
                        if (actor is Guid owner && !await db.Set<BackgroundJob>().AnyAsync(x => x.Kind == EmbeddingReindexHandler.JobKind && x.SubjectId == subject, stoppingToken)) jobs.Enqueue(owner, null, subject, EmbeddingReindexHandler.JobKind, $"重建 {target.Model} 檢索索引");
                    }
                    await db.SaveChangesAsync(stoppingToken);
                    if (clock.GetUtcNow() - cleanedAt > TimeSpan.FromHours(1)) { await scope.ServiceProvider.GetRequiredService<EmbeddingLifecycle>().CleanupExpiredAsync(stoppingToken); cleanedAt = clock.GetUtcNow(); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { LogDeferred(logger, error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    [LoggerMessage(EventId = 2003, EventName = "embedding.bootstrap_deferred", Level = LogLevel.Warning, Message = "索引啟動或保留期限工作未完成。原因：{Reason}")]
    private static partial void LogDeferred(ILogger logger, string reason);
}
