using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Collaboration;

/// <summary>
/// 「容器（專案、評估集）有進行中的工作就不能刪」的資料庫鎖：開始工作取共用鎖，彼此不等待；刪除取互斥鎖，等正在開始的工作提交後才檢查，檢查到提交之間也不會有新工作。
/// 只靠刪除端檢查不夠：RCSI 讀不到對方還沒提交的工作，對方也可能已通過存活檢查、之後照樣寫入。兩邊都要在交易的第一步取鎖（<see cref="ApplicationLocks"/>），並在取鎖之後才檢查，鎖隨交易釋放。
/// </summary>
[Scoped]
public sealed class ContainerLock(AppDbContext db)
{
    // 持有者只做幾句 SQL，等這麼久表示異常，交給使用者重試。
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public Task<bool> TryStartWorkAsync(Guid container, CancellationToken ct)
        => db.Database.TryAcquireApplicationLockAsync(Name(container), Timeout, ct, shared: true);

    public Task<bool> TryDeleteAsync(Guid container, CancellationToken ct)
        => db.Database.TryAcquireApplicationLockAsync(Name(container), Timeout, ct);

    private static string Name(Guid container) => $"container:{container:N}";
}
