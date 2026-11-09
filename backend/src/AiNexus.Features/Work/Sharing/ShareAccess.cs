using AiNexus.Data.App;
using AiNexus.Data.App.Artifacts;
using AiNexus.Data.App.Sharing;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Sharing;

/// <summary>
/// 誰能看見分享。分享綁定帳號，且獨立於來源本身的 ACL 檢查：分享必須仍有效並寄給該讀者、
/// 來源必須仍存在，且擁有者仍具備來源所屬的功能。
/// </summary>
[Scoped]
internal sealed class ShareAccess(AppDbContext db, AccessService features, TimeProvider clock)
{
    /// <summary>對話來源的種類；成果來源沿用 <see cref="Artifact.Kind"/>。</summary>
    public const string ConversationKind = "conversation";

    public async Task<Result<ShareLink>> RequireAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var row = await db.ShareLinks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsRevoked && x.ExpiresAt > now && (x.OwnerId == actor || db.ShareRecipients.Any(r => r.ShareId == id && r.UserId == actor)), ct);
        if (row is null) return SharingErrors.Unavailable;
        if (!await SourceExistsAsync(row, ct)) return SharingErrors.Unavailable;
        if (!await HasSourceFeatureAsync(row.OwnerId, row.Kind, ct)) return SharingErrors.Unavailable;
        return row;
    }

    public async Task<bool> SourceExistsAsync(ShareLink row, CancellationToken ct) => row.Kind == ConversationKind
        ? await db.Conversations.AnyAsync(x => x.Id == row.SourceId && x.OwnerId == row.OwnerId, ct)
        : await db.Resources.AnyAsync(x => x.Id == row.SourceId && x.OwnerId == row.OwnerId && x.Kind == Artifact.Kind, ct);

    /// <summary>只留來源仍存在的分享（規則同 <see cref="SourceExistsAsync"/>）；放在查詢裡，分頁的總數才正確。</summary>
    public IQueryable<ShareLink> WithSources(IQueryable<ShareLink> links) => links.Where(x =>
        (x.Kind == ConversationKind && db.Conversations.Any(c => c.Id == x.SourceId && c.OwnerId == x.OwnerId))
        || (x.Kind != ConversationKind && db.Resources.Any(r => r.Id == x.SourceId && r.OwnerId == x.OwnerId && r.Kind == Artifact.Kind)));

    public Task<bool> HasSourceFeatureAsync(Guid user, string kind, CancellationToken ct)
        => features.HoldsAsync(user, kind == ConversationKind ? FeatureCatalog.Chat : FeatureCatalog.Artifacts, ct);

    public async Task<ShareDto> DescribeAsync(Guid actor, ShareLink x, CancellationToken ct) => (await DescribeAllAsync(actor, [x], ct))[0];

    /// <summary>依原順序描述多筆分享，最多兩次查詢；只有寄件者看得到收件人清單。</summary>
    public async Task<IReadOnlyList<ShareDto>> DescribeAllAsync(Guid actor, IReadOnlyList<ShareLink> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var ownerIds = rows.Select(x => x.OwnerId).Distinct().ToArray();
        var owners = await db.Users.Where(u => ownerIds.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName }).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var sent = rows.Where(x => x.OwnerId == actor).Select(x => x.Id).ToArray();
        var recipients = sent.Length == 0 ? [] : (await (from r in db.ShareRecipients join u in db.Users on r.UserId equals u.Id where sent.Contains(r.ShareId) select new { r.ShareId, u.DisplayName }).ToListAsync(ct))
            .GroupBy(x => x.ShareId).ToDictionary(g => g.Key, g => g.Select(x => x.DisplayName).ToList());
        return rows.Select(x => new ShareDto(x.Id, x.Kind, x.Title, owners.TryGetValue(x.OwnerId, out var owner) ? owner : throw new InvalidOperationException("Share owner is missing."),
            x.OwnerId == actor, x.IsRevoked, x.ExpiresAt, x.CreatedAt, x.OwnerId == actor ? recipients.GetValueOrDefault(x.Id) ?? [] : [], x.IncludeAttachments)).ToArray();
    }
}
