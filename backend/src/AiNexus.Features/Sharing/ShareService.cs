using AiNexus.Features.Attachments;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Sharing;

/// <summary>Share revocation and expiry purge; shares of a deleted source stop working immediately.</summary>
public sealed class ShareService(NexusDbContext db, TimeProvider clock)
{
    /// <summary>Revokes every share of a source and drops its file grants. Caller owns the transaction.</summary>
    internal async Task RevokeSourceAsync(string kind, Guid id, CancellationToken ct)
    {
        var ids = db.Set<ShareLink>().Where(x => x.Kind == kind && x.SourceId == id).Select(x => x.Id);
        await db.Set<AttachmentReference>().Where(x => ids.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
        await db.Set<ShareLink>().Where(x => x.Kind == kind && x.SourceId == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsRevoked, true).SetProperty(x => x.SnapshotJson, ""), ct);
    }

    /// <summary>Drops file grants of expired or revoked shares and erases the snapshot text of expired ones.</summary>
    public async Task PurgeExpiredAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ids = db.Set<ShareLink>().Where(x => x.ExpiresAt <= now || x.IsRevoked).Select(x => x.Id);
        await db.Set<AttachmentReference>().Where(x => ids.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
        await db.Set<ShareLink>().Where(x => x.ExpiresAt <= now && x.SnapshotJson != "").ExecuteUpdateAsync(p => p.SetProperty(x => x.SnapshotJson, ""), ct);
        await tx.CommitAsync(ct);
    }
}

public sealed class ShareCleanupWorker(IServiceScopeFactory scopes, StorageReadiness storage, ILogger<ShareCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!storage.Configured) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(20));
        do { try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<ShareService>().PurgeExpiredAsync(ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; } catch (Exception ex) { logger.LogWarning("Share cleanup deferred ({ErrorType}).", ex.GetType().Name); } } while (await timer.WaitForNextTickAsync(ct));
    }
}
