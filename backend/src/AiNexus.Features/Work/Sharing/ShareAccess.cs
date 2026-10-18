using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Sharing;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Sharing;

/// <summary>
/// 誰能看見分享。分享綁定帳號，且獨立於來源本身的 ACL 檢查：分享必須仍有效並寄給該讀者、
/// 來源必須仍存在，且擁有者仍具備來源所屬的功能。
/// </summary>
internal sealed class ShareAccess(AppDbContext db, AccessService features, TimeProvider clock)
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
        : await db.Set<Resource>().AnyAsync(x => x.Id == row.SourceId && x.OwnerId == row.OwnerId && x.Kind == "artifact", ct);

    /// <summary><paramref name="rows"/> 中來源仍存在的列（見 <see cref="SourceExistsAsync"/>），維持原順序，最多兩次查詢。</summary>
    public async Task<IReadOnlyList<ShareLink>> WithSourcesAsync(IReadOnlyList<ShareLink> rows, CancellationToken ct)
    {
        var conversations = rows.Where(x => x.Kind == "conversation").Select(x => x.SourceId).Distinct().ToArray();
        var artifacts = rows.Where(x => x.Kind != "conversation").Select(x => x.SourceId).Distinct().ToArray();
        var owners = new HashSet<(Guid Source, Guid Owner)>();
        if (conversations.Length > 0)
            owners.UnionWith((await db.Conversations.Where(x => conversations.Contains(x.Id)).Select(x => new { x.Id, x.OwnerId }).ToListAsync(ct)).Select(x => (x.Id, x.OwnerId)));
        if (artifacts.Length > 0)
            owners.UnionWith((await db.Set<Resource>().Where(x => artifacts.Contains(x.Id) && x.Kind == "artifact").Select(x => new { x.Id, x.OwnerId }).ToListAsync(ct)).Select(x => (x.Id, x.OwnerId)));
        return rows.Where(x => owners.Contains((x.SourceId, x.OwnerId))).ToArray();
    }

    public async Task<bool> HasSourceFeatureAsync(Guid user, string kind, CancellationToken ct)
        => (await features.ForUserAsync(user, ct)).Features.Any(x => x.Id == (kind == "conversation" ? FeatureIds.Chat : FeatureIds.Artifacts));

    public async Task<ShareDto> DescribeAsync(Guid actor, ShareLink x, CancellationToken ct)
    {
        var owner = await db.Users.Where(u => u.Id == x.OwnerId).Select(u => u.DisplayName).SingleAsync(ct);
        // 只有寄件者能看到完整的收件人清單
        var recipients = actor == x.OwnerId ? await (from r in db.Set<ShareRecipient>() join u in db.Users on r.UserId equals u.Id where r.ShareId == x.Id select u.DisplayName).ToListAsync(ct) : [];
        return new(x.Id, x.Kind, x.Title, owner, x.OwnerId == actor, x.IsRevoked, x.ExpiresAt, x.CreatedAt, recipients, x.IncludeAttachments);
    }

    /// <summary><see cref="DescribeAsync"/> 的清單版本，依原順序回傳，最多兩次查詢。</summary>
    public async Task<IReadOnlyList<ShareDto>> DescribeAllAsync(Guid actor, IReadOnlyList<ShareLink> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var ownerIds = rows.Select(x => x.OwnerId).Distinct().ToArray();
        var owners = await db.Users.Where(u => ownerIds.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName }).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        // 只有寄件者能看到完整的收件人清單
        var sent = rows.Where(x => x.OwnerId == actor).Select(x => x.Id).ToArray();
        var recipients = sent.Length == 0 ? [] : (await (from r in db.Set<ShareRecipient>() join u in db.Users on r.UserId equals u.Id where sent.Contains(r.ShareId) select new { r.ShareId, u.DisplayName }).ToListAsync(ct))
            .GroupBy(x => x.ShareId).ToDictionary(g => g.Key, g => g.Select(x => x.DisplayName).ToList());
        return rows.Select(x => new ShareDto(x.Id, x.Kind, x.Title, owners.TryGetValue(x.OwnerId, out var owner) ? owner : throw new InvalidOperationException("Share owner is missing."),
            x.OwnerId == actor, x.IsRevoked, x.ExpiresAt, x.CreatedAt, x.OwnerId == actor ? recipients.GetValueOrDefault(x.Id) ?? [] : [], x.IncludeAttachments)).ToArray();
    }
}
