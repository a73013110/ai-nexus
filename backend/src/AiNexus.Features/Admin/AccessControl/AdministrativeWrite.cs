using AiNexus.Data.App;
using System.Data;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 一次行政寫入的對象：稽核的資源與 key，以及變更前後各讀一次的快照（JSON 形狀即稽核合約）。
/// 同一資源的新增、修改、刪除與其他動作共用一份宣告（如 <c>CardRules.Target</c>）；key 與資源不是一對一時才直接用建構子。
/// </summary>
public sealed record AuditTarget(Guid? Resource, string Key, Func<CancellationToken, Task<object?>> Snapshot, Guid? Subject = null)
{
    /// <summary>以資源 ID 當稽核 key 的一般情況。</summary>
    public static AuditTarget Of(Guid resource, Func<CancellationToken, Task<object?>> snapshot) => new(resource, resource.ToString(), snapshot);

    /// <summary>沒有單一資源的寫入（如匯入、排序、設定）：只有稽核 key。</summary>
    public static AuditTarget Of(string key, Func<CancellationToken, Task<object?>> snapshot) => new(null, key, snapshot);

    /// <summary><see cref="AdministrativeWrite.BeginAsync"/> 要先取得的鎖；預設不取。只給跨列、又沒有約束能擋的規則，範圍取規則涵蓋的最小單位（如一張卡）。</summary>
    public IReadOnlyList<string> Locks { get; init; } = [];

    /// <summary>會改變「誰持有什麼功能」的寫入要守住的權限，由 <see cref="KeepingAccess"/> 宣告。</summary>
    public AccessGuard? Guard { get; init; }

    /// <summary>一個請求逐筆套用多筆資料時，這一筆自己的稽核，由 <see cref="AuditedPerItem"/> 宣告。</summary>
    public ItemAudit? Item { get; init; }

    /// <summary>同一個寫入對象，加上要取得的鎖。</summary>
    public AuditTarget Locking(params string[] names) => this with { Locks = [.. Locks, .. names] };

    /// <summary>
    /// 寫入會改變授權（帳號、角色、群組、功能）：取授權鎖，開始時確認 <paramref name="actor"/> 持有 <paramref name="feature"/>，
    /// 提交前再確認變更後仍持有，否則回滾並回 <see cref="AccessControlErrors.Lockout"/>。
    /// 授權鎖讓這類寫入排隊，兩位管理員才不會互相撤權，又都在對方提交前通過檢查。
    /// </summary>
    public AuditTarget KeepingAccess(Guid actor, string feature) => Locking(AccessControlLocks.Authorization) with { Guard = new(actor, feature) };

    /// <summary>
    /// 一個請求逐筆套用多筆資料（如 AD 匯入）：每筆各寫一筆 <paramref name="action"/> 稽核，一筆失敗不影響其他筆，請求本身仍可回成功。
    /// 失敗由呼叫端在回滾後以 <see cref="AdministrativeWrite.RecordItemFailureAsync"/> 記下。
    /// </summary>
    public AuditTarget AuditedPerItem(AuditAction action, Guid actor) => this with { Item = new(action, actor) };
}

/// <summary>見 <see cref="AuditTarget.KeepingAccess"/>。</summary>
public sealed record AccessGuard(Guid Actor, string Feature);

/// <summary>見 <see cref="AuditTarget.AuditedPerItem"/>。</summary>
public sealed record ItemAudit(AuditAction Action, Guid Actor);

/// <summary>
/// 管理端寫入的資料稽核：變更與其前後快照共用同一個交易，變更與稽核要嘛一起成功、要嘛一起回滾。
/// 快照由擁有該資料的 slice 提供（<see cref="AuditTarget"/>），所以這裡不依賴任何上層模組。
/// </summary>
public sealed partial class AdministrativeWrite(AppDbContext db, AccessService access, AdministrativeWriteLock writes, AuditTrail audit, IOptions<AccessControlOptions> options, TimeProvider time, ILogger<AdministrativeWrite> logger)
{
    /// <summary>
    /// 開交易、依 <see cref="AuditTarget.Locks"/> 取鎖並記下前值。業務邏輯寫在這之後，最後呼叫 <see cref="AdministrativeTransaction.CommitAsync"/>。
    /// 權限由端點的 <c>RequireFeature</c> 在同一個請求檢查過，只有 <see cref="AuditTarget.KeepingAccess"/> 才在鎖內重查，不符回 <see cref="AccessControlErrors.FeatureForbidden"/>。
    /// 只用在有請求稽核的端點：沒有提交的保護靠它。等不到鎖回 <see cref="AccessControlErrors.WriteBusy"/>。
    /// </summary>
    public async Task<Result<AdministrativeTransaction>> BeginAsync(AuditTarget target, CancellationToken ct)
    {
        // 隔離等級明確寫出：連線池重用的連線可能帶著別的交易留下的等級。
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            Error? refusal = !await TryAcquireLocksAsync(target, ct) ? AccessControlErrors.WriteBusy
                : !await HoldsAsync(target, ct) ? AccessControlErrors.FeatureForbidden
                : null;
            if (refusal is not null)
            {
                await transaction.DisposeAsync();
                return refusal;
            }
            // 取得鎖之後才計時：等別人不算自己佔用。
            var startedAt = time.GetTimestamp();
            var before = await SnapshotAsync(target, ct);
            if (target.Item is null) audit.Describe(target.Resource, target.Subject, new { resourceKey = target.Key, before });
            return new AdministrativeTransaction(this, db, transaction, target, before, startedAt);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    /// <summary>逐筆稽核（<see cref="AuditTarget.AuditedPerItem"/>）的一筆失敗：交易已回滾，前值重讀即可，錯誤代碼寫進這筆稽核。</summary>
    public async Task RecordItemFailureAsync(AuditTarget target, Error failure, CancellationToken ct)
    {
        var item = target.Item ?? throw new InvalidOperationException("Only item-audited writes record their own failures.");
        db.ChangeTracker.Clear();
        audit.Record(item.Action, item.Actor, target.Resource, target.Subject, new { resourceKey = target.Key[..Math.Min(target.Key.Length, 64)], before = await SnapshotAsync(target, ct), failureCode = failure.Code }, failure.Code);
        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> TryAcquireLocksAsync(AuditTarget target, CancellationToken ct)
    {
        // 依名稱排序：同時取多把鎖的寫入順序一致，不會互等成死結。
        foreach (var name in target.Locks.Order(StringComparer.Ordinal))
            if (!await writes.TryAcquireAsync(name, ct)) return false;
        return true;
    }

    /// <summary>沒有宣告 <see cref="AuditTarget.Guard"/> 時一律為真。在交易內讀 SQL，不信任請求一開始授權時的快取結果。</summary>
    internal async Task<bool> HoldsAsync(AuditTarget target, CancellationToken ct)
    {
        if (target.Guard is not { } guard) return true;
        access.Invalidate();
        return await access.HoldsAsync(guard.Actor, guard.Feature, ct);
    }

    /// <summary>把後值記進稽核；提交前呼叫，後值與變更同一個交易。</summary>
    internal async Task NoteAfterAsync(AuditTarget target, object? before, CancellationToken ct)
    {
        var after = await SnapshotAsync(target, ct);
        if (target.Item is not { } item) audit.Describe(details: new { after });
        else audit.Record(item.Action, item.Actor, target.Resource, target.Subject, new { resourceKey = target.Key, before, after });
    }

    /// <summary>交易已結束。沒有提交時清掉未提交的異動：之後補寫失敗稽核才不會一併寫入。</summary>
    internal void NoteFinished(AuditTarget target, long startedAt, bool committed)
    {
        var held = time.GetElapsedTime(startedAt);
        // 佔到別人等鎖的上限，等的人就會收到 503：通常是耗時工作放在開始與提交之間。
        if (held.TotalSeconds >= options.Value.WriteLockTimeoutSeconds) LogHeldTooLong(logger, target.Item?.Action.Name ?? audit.ActionName ?? "unknown", (long)held.TotalMilliseconds);
        if (target.Guard is not null) access.Invalidate();
        if (committed) return;
        db.ChangeTracker.Clear();
        // 逐筆稽核的請求，一筆失敗後仍可回成功。
        if (target.Item is null) audit.NoteRollback();
    }

    [LoggerMessage(EventId = 1004, EventName = "admin_write.held_too_long", Level = LogLevel.Warning, Message = "Administrative write {AuditAction} held its transaction for {ElapsedMs} ms.")]
    private static partial void LogHeldTooLong(ILogger logger, string auditAction, long elapsedMs);

    // 鎖只排隊行政寫入；本機登入、本人改資料等鎖外的寫入仍可能撞上同一列，由唯一索引、並行控制欄位或死結偵測擋下。
    // 這是時機問題、不是程式錯誤，重送就會成功，所以回 409 而不是 500。不自動重試：要讓使用者看過新的資料再決定。
    internal static bool IsWriteConflict(Exception error) => error is DbUpdateConcurrencyException
        || (error as DbUpdateException)?.InnerException is SqlException { Number: 2601 or 2627 or 1205 }
        || error is SqlException { Number: 1205 };

    // 變更可能繞過追蹤（ExecuteUpdate、procedure）：查到追蹤中的實體會得到舊值，稽核卻不會報錯。
    // 所以快照一律不追蹤，不只靠每個 slice 記得寫 AsNoTracking。
    private async Task<object?> SnapshotAsync(AuditTarget target, CancellationToken ct)
    {
        var tracking = db.ChangeTracker.QueryTrackingBehavior;
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        try { return await target.Snapshot(ct); }
        finally { db.ChangeTracker.QueryTrackingBehavior = tracking; }
    }
}

/// <summary>
/// 行政寫入的具名鎖：範圍由規則決定（一張卡、整棵單位樹），名稱集中在各模組的 <c>Locks</c> 類別。
/// 用資料庫鎖而不是程序內鎖，本機與 IIS 共用資料庫時一樣有效。必須是交易的第一個動作，說明見 <see cref="ApplicationLocks"/>。
/// </summary>
public sealed class AdministrativeWriteLock(AppDbContext db, IOptions<AccessControlOptions> options)
{
    /// <summary>取得指定名稱的鎖，持有到目前的交易結束；等到逾時回 <c>false</c>。</summary>
    public Task<bool> TryAcquireAsync(string name, CancellationToken ct) => db.Database.TryAcquireApplicationLockAsync(
        name, TimeSpan.FromSeconds(options.Value.WriteLockTimeoutSeconds), ct);

    /// <summary>同上，等到逾時拋 <see cref="AccessControlErrors.WriteBusy"/>：給不回傳 <see cref="Result"/> 的路徑（背景工作、回傳實體的服務）。</summary>
    public async Task AcquireAsync(string name, CancellationToken ct)
    {
        if (!await TryAcquireAsync(name, ct)) throw new ExternalServiceException(AccessControlErrors.WriteBusy);
    }
}
