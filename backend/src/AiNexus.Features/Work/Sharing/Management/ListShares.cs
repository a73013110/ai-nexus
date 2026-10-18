using AiNexus.Data.App;
using AiNexus.Data.App.Sharing;
using AiNexus.Features.Admin.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Sharing.Management;

/// <summary>使用者寄出的分享，或寄給使用者且來源仍存在的有效分享。</summary>
internal sealed class ListShares(AppDbContext db, ShareAccess shares, TimeProvider clock)
{
    private const int PageSize = 100;

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (bool? sent, ICurrentUser user, ListShares handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, sent ?? false, ct)));

    public async Task<IReadOnlyList<ShareDto>> HandleAsync(Guid actor, bool sent, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = await db.Set<ShareLink>().AsNoTracking()
            .Where(x => sent ? x.OwnerId == actor : !x.IsRevoked && x.ExpiresAt > now && db.Set<ShareRecipient>().Any(r => r.ShareId == x.Id && r.UserId == actor))
            .OrderByDescending(x => x.CreatedAt).Take(PageSize).ToListAsync(ct);
        return await shares.DescribeAllAsync(actor, sent ? rows : await shares.WithSourcesAsync(rows, ct), ct);
    }
}
