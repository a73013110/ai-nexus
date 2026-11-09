using AiNexus.Data.App;
using AiNexus.Data.App.Sharing;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Sharing.Management;

/// <summary>使用者寄出的分享，或寄給使用者且來源仍存在的有效分享；搜尋與分頁走共用的 <see cref="TableRequest"/>（ADR 0026）。</summary>
internal sealed class ListShares(AppDbContext db, ShareAccess shares, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (bool? sent, [AsParameters] TableRequest table, ICurrentUser user, ListShares handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, sent ?? false, table, ct)).Audited(SharingAudit.ShareRead);

    public async Task<Result<TablePage<ShareDto>>> HandleAsync(Guid actor, bool sent, TableRequest table, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = sent
            ? db.ShareLinks.AsNoTracking().Where(x => x.OwnerId == actor)
            : shares.WithSources(db.ShareLinks.AsNoTracking().Where(x => !x.IsRevoked && x.ExpiresAt > now && db.ShareRecipients.Any(r => r.ShareId == x.Id && r.UserId == actor)));
        var page = await query.ToTablePageAsync(table, Columns, x => x, ct);
        if (!page.IsSuccess) return page.Error;
        return page.Value.With(await shares.DescribeAllAsync(actor, page.Value.Items, ct));
    }

    private static readonly TableColumns<ShareLink> Columns = new TableColumns<ShareLink>()
        .Text("title", x => x.Title)
        .Range("createdAt", x => x.CreatedAt)
        .Key(x => x.Id)
        .DefaultSort("createdAt", descending: true);
}
