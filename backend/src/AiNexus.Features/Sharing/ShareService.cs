using AiNexus.Data.App;
using AiNexus.Data.App.Attachments;
using AiNexus.Data.App.Sharing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Sharing;

/// <summary>分享的撤銷與過期清除；來源被刪除時，其分享立即失效。</summary>
public sealed class ShareService(AppDbContext db, TimeProvider clock)
{
    /// <summary>撤銷來源的所有分享並移除其檔案授權。交易由呼叫端管理。</summary>
    internal async Task RevokeSourceAsync(string kind, Guid id, CancellationToken ct)
    {
        var ids = db.Set<ShareLink>().Where(x => x.Kind == kind && x.SourceId == id).Select(x => x.Id);
        await db.Set<ResourceAttachment>().Where(x => ids.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
        await db.Set<ShareLink>().Where(x => x.Kind == kind && x.SourceId == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsRevoked, true).SetProperty(x => x.SnapshotJson, ""), ct);
    }

    /// <summary>移除已過期或已撤銷分享的檔案授權，並清空已過期分享的快照文字。</summary>
    public async Task PurgeExpiredAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ids = db.Set<ShareLink>().Where(x => x.ExpiresAt <= now || x.IsRevoked).Select(x => x.Id);
        await db.Set<ResourceAttachment>().Where(x => ids.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
        await db.Set<ShareLink>().Where(x => x.ExpiresAt <= now && x.SnapshotJson != "").ExecuteUpdateAsync(p => p.SetProperty(x => x.SnapshotJson, ""), ct);
        await tx.CommitAsync(ct);
    }
}

public sealed partial class ShareCleanupWorker(IServiceScopeFactory scopes, StorageReadiness storage, ILogger<ShareCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!storage.Configured) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(20));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ShareService>().PurgeExpiredAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogDeferred(logger, ex.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    [LoggerMessage(EventId = 6101, EventName = "share.cleanup_deferred", Level = LogLevel.Warning, Message = "Share cleanup deferred ({ErrorType}).")]
    private static partial void LogDeferred(ILogger logger, string errorType);
}
