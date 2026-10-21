using AiNexus.Data.App;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Files.Storage;

/// <summary>管理員設定的個人儲存上限；<c>null</c> 代表沿用群組或預設上限。</summary>
[ValidatedInHandler("The limit is checked inside the audited administrative transaction (SetUserAttachmentStorage).")]
public sealed record AttachmentStorageLimitRequest(long? LimitBytes);

public sealed class AttachmentQuota(AppDbContext db, ISqlDatabase<AppDbContext> sql, IOptions<FilesOptions> options)
{
    /// <summary>沒有個人與群組上限時的預設儲存上限（設定）。</summary>
    public long DefaultLimitBytes => options.Value.DefaultOwnerLimitBytes;

    /// <summary>
    /// 已用、上限與上限來源，規則在 <c>files.udf_AttachmentStorage</c>（<c>operations.usp_ListAdminUsers</c> 讀同一份）。
    /// 經 <see cref="ISqlDatabase{TDatabase}"/> 共用 EF 的連線與目前交易，所以 <see cref="ReserveAsync"/> 在 <see cref="LockOwnerAsync"/> 之後讀到的是鎖定後的最新已提交資料。
    /// </summary>
    public Task<AttachmentStorageDto> ForAsync(Guid owner, CancellationToken ct) => sql.QuerySingleAsync<AttachmentStorageDto>("""
        SELECT UsedBytes, LimitBytes, RemainingBytes, PersonalLimitBytes, GroupLimitBytes, DefaultLimitBytes, LimitSource
        FROM [files].[udf_AttachmentStorage](@DefaultLimitBytes) WHERE OwnerId = @Owner
        """, new { Owner = owner, DefaultLimitBytes }, cancellationToken: ct);

    // 需在呼叫端的交易內呼叫，並在檢查位元組或綁定、解除引用之前執行。
    // 資料庫列鎖讓同一個 SQL 資料庫的所有程序彼此協調。
    public Task<int> LockOwnerAsync(Guid owner, CancellationToken ct) => db.Users.Where(x => x.Id == owner)
        .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);

    public async Task<Result> ReserveAsync(Guid owner, long bytes, CancellationToken ct)
    {
        await LockOwnerAsync(owner, ct);
        var quota = await ForAsync(owner, ct);
        return bytes > quota.RemainingBytes ? FilesErrors.Quota : Result.Success;
    }
}
