using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Data.App.Sharing;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Sharing.Management;

/// <summary>擁有者撤銷分享：收件人立即失去存取權與檔案授權，快照文字也會被清除。</summary>
internal sealed class RevokeShare(AppDbContext db, ShareWriteLock writes)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, RevokeShare handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync());

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var row = await db.Set<ShareLink>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor, ct);
            if (row is null) return SharingErrors.Unavailable;
            row.IsRevoked = true;
            row.SnapshotJson = "";
            await db.Set<ResourceAttachment>().Where(x => x.ResourceId == id).ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(new() { OwnerId = actor, Action = "share.revoked", ResourceId = id, Result = "revoked" });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success;
        }
        finally
        {
            writes.Gate.Release();
        }
    }
}
