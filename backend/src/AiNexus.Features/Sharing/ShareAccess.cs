using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Sharing;

/// <summary>
/// Who may see a share. Shares are bound to accounts and checked independently of the source's own ACL: the share must
/// be live and addressed to the reader, its source must still exist, and the owner must still have the source's feature.
/// </summary>
internal sealed class ShareAccess(NexusDbContext db, AccessService features, TimeProvider clock)
{
    public async Task<Result<ShareLink>> RequireAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var row = await db.Set<ShareLink>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsRevoked && x.ExpiresAt > now && (x.OwnerId == actor || db.Set<ShareRecipient>().Any(r => r.ShareId == id && r.UserId == actor)), ct);
        if (row is null) return SharingErrors.Unavailable;
        if (!await SourceExistsAsync(row, ct)) return SharingErrors.Unavailable;
        if (!await HasSourceFeatureAsync(row.OwnerId, row.Kind, ct)) return SharingErrors.Unavailable;
        return row;
    }

    public async Task<bool> SourceExistsAsync(ShareLink row, CancellationToken ct) => row.Kind == "conversation"
        ? await db.Conversations.AnyAsync(x => x.Id == row.SourceId && x.OwnerId == row.OwnerId, ct)
        : await db.Set<WorkspaceResource>().AnyAsync(x => x.Id == row.SourceId && x.OwnerId == row.OwnerId && x.Kind == "artifact", ct);

    public async Task<bool> HasSourceFeatureAsync(Guid user, string kind, CancellationToken ct)
        => (await features.ForUserAsync(user, ct)).Features.Any(x => x.Id == (kind == "conversation" ? FeatureIds.Chat : FeatureIds.Artifacts));

    public async Task<ShareDto> DescribeAsync(Guid actor, ShareLink x, CancellationToken ct)
    {
        var owner = await db.Users.Where(u => u.Id == x.OwnerId).Select(u => u.DisplayName).SingleAsync(ct);
        // Only the sender sees the full recipient list.
        var recipients = actor == x.OwnerId ? await (from r in db.Set<ShareRecipient>() join u in db.Users on r.UserId equals u.Id where r.ShareId == x.Id select u.DisplayName).ToListAsync(ct) : [];
        return new(x.Id, x.Kind, x.Title, owner, x.OwnerId == actor, x.IsRevoked, x.ExpiresAt, x.CreatedAt, recipients, x.IncludeAttachments);
    }
}
