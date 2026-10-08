using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Sharing;

/// <summary>The owner withdraws a share: recipients lose it and its file grants at once, and the snapshot text is erased.</summary>
internal static class RevokeShare
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, NexusDbContext db, ShareWriteLock writes, CancellationToken ct) =>
            (await HandleAsync(db, writes, user.Id, id, ct)).ToHttpResult());

    public static async Task<Result> HandleAsync(NexusDbContext db, ShareWriteLock writes, Guid actor, Guid id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var row = await db.Set<ShareLink>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor, ct);
            if (row is null) return SharingErrors.Unavailable;
            row.IsRevoked = true; row.SnapshotJson = "";
            await db.Set<AttachmentReference>().Where(x => x.ResourceId == id).ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(new() { OwnerId = actor, Action = "share.revoked", ResourceId = id, Result = "revoked" });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Result.Success;
        }
        finally { writes.Gate.Release(); }
    }
}
