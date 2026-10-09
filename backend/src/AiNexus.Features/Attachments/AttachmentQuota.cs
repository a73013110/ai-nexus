using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments;

/// <summary>A personal storage limit set by an administrator; <c>null</c> falls back to the group or default limit.</summary>
public sealed record AttachmentStorageLimitRequest(long? LimitBytes);

public sealed class AttachmentQuota(NexusDbContext db, AccessService access, IOptions<AttachmentOptions> options)
{
    // Personal overrides deliberately take precedence over inherited limits, including group caps.
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

    // Call inside the caller's transaction before checking bytes or binding/removing a reference.
    // The database row lock coordinates all processes using the same SQL database.
    public Task<int> LockOwnerAsync(Guid owner, CancellationToken ct) => db.Users.Where(x => x.Id == owner)
        .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);

    public async Task ReserveAsync(Guid owner, long bytes, CancellationToken ct)
    {
        await LockOwnerAsync(owner, ct);
        var quota = await ForAsync(owner, ct);
        if (bytes > quota.RemainingBytes) throw new ApiException(413, "attachment_quota", "附件容量不足，請刪除未引用的檔案，或聯絡管理員調整個人容量上限。");
    }
}
