using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Collaboration;

/// <summary>
/// 依資源排隊的資料庫鎖：版本化儲存、ACL 變更與刪除要先取得，不同資源互不等待。要是交易的第一個動作（<see cref="ApplicationLocks"/>），並在取鎖之後才檢查資源是否還在，鎖隨交易釋放。
/// 同時要取 <see cref="ContainerLock"/> 時先取這把。
/// </summary>
[Scoped]
public sealed class ResourceWriteLock(AppDbContext db)
{
    // 持有者只做幾句 SQL，等這麼久表示異常，交給使用者重試。
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public Task<bool> TryAcquireAsync(Guid resource, CancellationToken ct) => db.Database.TryAcquireApplicationLockAsync($"resource:{resource:N}", Timeout, ct);
}
