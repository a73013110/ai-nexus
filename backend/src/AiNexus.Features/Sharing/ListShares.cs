using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Sharing;

/// <summary>Shares the user sent, or live shares addressed to them whose source still exists.</summary>
internal sealed class ListShares(NexusDbContext db, ShareAccess shares, TimeProvider clock)
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
