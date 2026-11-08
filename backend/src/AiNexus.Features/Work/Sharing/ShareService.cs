using AiNexus.Data.App;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Sharing;

/// <summary>分享的撤銷與過期清除；來源被刪除時，其分享立即失效。</summary>
[Scoped]
public sealed class ShareService(AppDbContext db, TimeProvider clock)
{
    /// <summary>撤銷來源的所有分享並移除其檔案授權。交易由呼叫端管理。</summary>
    internal async Task RevokeSourceAsync(string kind, Guid id, CancellationToken ct)
    {
        var rows = db.ShareLinks.Where(x => x.Kind == kind && x.SourceId == id);
        await db.ResourceAttachments.Where(x => rows.Select(s => s.Id).Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
        await rows.ExecuteUpdateAsync(p => p.SetProperty(x => x.IsRevoked, true).SetProperty(x => x.SnapshotJson, ""), ct);
    }

    /// <summary>移除已過期或已撤銷分享的檔案授權，並清空已過期分享的快照文字；回傳異動的列數。</summary>
    public async Task<int> PurgeExpiredAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ids = db.ShareLinks.Where(x => x.ExpiresAt <= now || x.IsRevoked).Select(x => x.Id);
        var grants = await db.ResourceAttachments.Where(x => ids.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
        var snapshots = await db.ShareLinks.Where(x => x.ExpiresAt <= now && x.SnapshotJson != "").ExecuteUpdateAsync(p => p.SetProperty(x => x.SnapshotJson, ""), ct);
        await tx.CommitAsync(ct);
        return grants + snapshots;
    }
}
