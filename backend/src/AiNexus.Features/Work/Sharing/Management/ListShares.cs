using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Sharing.Management;

/// <summary>使用者寄出的分享，或寄給使用者且來源仍存在的有效分享。</summary>
internal sealed class ListShares(AppDbContext db, ShareAccess shares, TimeProvider clock) : IEndpointSlice
{
    private const int PageSize = 100;

    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (bool? sent, ICurrentUser user, ListShares handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, sent ?? false, ct)).Audited(SharingAudit.ShareRead);

    public async Task<IReadOnlyList<ShareDto>> HandleAsync(Guid actor, bool sent, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = await db.ShareLinks.AsNoTracking()
            .Where(x => sent ? x.OwnerId == actor : !x.IsRevoked && x.ExpiresAt > now && db.ShareRecipients.Any(r => r.ShareId == x.Id && r.UserId == actor))
            .OrderByDescending(x => x.CreatedAt).Take(PageSize).ToListAsync(ct);
        return await shares.DescribeAllAsync(actor, sent ? rows : await shares.WithSourcesAsync(rows, ct), ct);
    }
}
