using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Sharing;

/// <summary>Shares the user sent, or live shares addressed to them whose source still exists.</summary>
internal static class ListShares
{
    private const int PageSize = 100;

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (bool? sent, ICurrentUser user, NexusDbContext db, ShareAccess shares, TimeProvider clock, CancellationToken ct) =>
            Results.Ok(await HandleAsync(db, shares, clock, user.Id, sent ?? false, ct)))
        .Produces<IReadOnlyList<ShareDto>>();

    public static async Task<IReadOnlyList<ShareDto>> HandleAsync(NexusDbContext db, ShareAccess shares, TimeProvider clock, Guid actor, bool sent, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = await db.Set<ShareLink>().AsNoTracking()
            .Where(x => sent ? x.OwnerId == actor : !x.IsRevoked && x.ExpiresAt > now && db.Set<ShareRecipient>().Any(r => r.ShareId == x.Id && r.UserId == actor))
            .OrderByDescending(x => x.CreatedAt).Take(PageSize).ToListAsync(ct);
        var result = new List<ShareDto>();
        foreach (var row in rows)
            if (sent || await shares.SourceExistsAsync(row, ct)) result.Add(await shares.DescribeAsync(actor, row, ct));
        return result;
    }
}
