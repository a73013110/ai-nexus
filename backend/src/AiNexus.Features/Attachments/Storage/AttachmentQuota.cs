using AiNexus.Features.AccessControl;
using AiNexus.Features.AccessControl.ModelPolicies;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments.Storage;

/// <summary>管理員設定的個人儲存上限；<c>null</c> 代表沿用群組或預設上限。</summary>
[ValidatedInHandler("The limit is checked inside the audited administrative transaction (SetUserAttachmentStorage).")]
public sealed record AttachmentStorageLimitRequest(long? LimitBytes);

public sealed class AttachmentQuota(NexusDbContext db, AccessService access, IOptions<AttachmentOptions> options)
{
    // 個人覆寫刻意優先於繼承的上限（包含群組上限）。
    public static long Effective(long? personal, long? group, long baseline) => personal ?? group ?? baseline;

    public async Task<AttachmentStorageDto> ForAsync(Guid owner, CancellationToken ct) => (await ForOwnersAsync([owner], ct))[owner];

    public async Task<IReadOnlyDictionary<Guid, AttachmentStorageDto>> ForOwnersAsync(IReadOnlyList<Guid> owners, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Where(x => owners.Contains(x.Id)).Select(x => new { x.Id, x.AttachmentLimitBytes }).ToListAsync(ct);
        var groups = await (from membership in access.GroupMemberships(owners) join policy in db.Set<GroupModelPolicy>() on membership.GroupId equals policy.GroupId
            select new { membership.UserId, policy.StoredAttachmentLimitBytes }).GroupBy(x => x.UserId)
            .Select(g => new { OwnerId = g.Key, Limit = g.Min(x => x.StoredAttachmentLimitBytes) }).ToDictionaryAsync(x => x.OwnerId, x => x.Limit, ct);
        var bytes = await db.Set<Attachment>().Where(x => owners.Contains(x.OwnerId)).GroupBy(x => x.OwnerId)
            .Select(g => new { OwnerId = g.Key, Bytes = g.Sum(x => x.Size) }).ToDictionaryAsync(x => x.OwnerId, x => x.Bytes, ct);
        return users.ToDictionary(x => x.Id, x =>
        {
            var personal = x.AttachmentLimitBytes; var group = groups.GetValueOrDefault(x.Id);
            var limit = Effective(personal, group, options.Value.DefaultOwnerLimitBytes); var used = bytes.GetValueOrDefault(x.Id);
            return new AttachmentStorageDto(used, limit, Math.Max(0, limit - used), personal, group, options.Value.DefaultOwnerLimitBytes, personal is not null ? "personal" : group is not null ? "group" : "default");
        });
    }

    // 需在呼叫端的交易內呼叫，並在檢查位元組或綁定、解除引用之前執行。
    // 資料庫列鎖讓同一個 SQL 資料庫的所有程序彼此協調。
    public Task<int> LockOwnerAsync(Guid owner, CancellationToken ct) => db.Users.Where(x => x.Id == owner)
        .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);

    public async Task<Result> ReserveAsync(Guid owner, long bytes, CancellationToken ct)
    {
        await LockOwnerAsync(owner, ct);
        var quota = await ForAsync(owner, ct);
        return bytes > quota.RemainingBytes ? AttachmentsErrors.Quota : Result.Success;
    }
}
