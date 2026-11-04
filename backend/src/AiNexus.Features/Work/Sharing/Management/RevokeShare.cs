using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Sharing.Management;

/// <summary>擁有者撤銷分享：收件人立即失去存取權與檔案授權，快照文字也會被清除。</summary>
internal sealed class RevokeShare(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, RevokeShare handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct)).Audited(SharingAudit.ShareRevoked);

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.ShareLinks.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor, ct);
        if (row is null) return SharingErrors.Unavailable;
        row.IsRevoked = true;
        row.SnapshotJson = "";
        await db.ResourceAttachments.Where(x => x.ResourceId == id).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Success;
    }
}
