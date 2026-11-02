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
/// 一次行政寫入的對象：端點所屬頁面的功能、稽核的資源與 key，以及變更前後各讀一次的快照（JSON 形狀即稽核合約）。
/// 同一資源的新增、修改、刪除與其他動作共用一份宣告（如 <c>CardRules.Target</c>）；key 與資源不是一對一時才直接用建構子。
/// </summary>
public sealed record AuditTarget(string Feature, Guid? Resource, string Key, Func<CancellationToken, Task<object?>> Snapshot, Guid? Subject = null)
{
    /// <summary>以資源 ID 當稽核 key 的一般情況。</summary>
    public static AuditTarget Of(string feature, Guid resource, Func<CancellationToken, Task<object?>> snapshot) => new(feature, resource, resource.ToString(), snapshot);

    /// <summary>沒有單一資源的寫入（如匯入、排序、設定）：只有稽核 key。</summary>
    public static AuditTarget Of(string feature, string key, Func<CancellationToken, Task<object?>> snapshot) => new(feature, null, key, snapshot);
}

/// <summary>
/// 管理端寫入的資料稽核：變更與其前後快照共用同一個交易，變更與稽核要嘛一起成功、要嘛一起回滾。
/// 快照由擁有該資料的 slice 提供（<see cref="AuditTarget"/>），執行者由呼叫端傳入，所以這裡不依賴任何上層模組。
/// </summary>
public sealed class AdministrativeWrite(AppDbContext db, AccessService access, AdministrativeWriteLock writes, AuditTrail audit)
{
    /// <summary>
    /// 在交易與 <see cref="AdministrativeWriteLock"/> 內執行變更，並於交易內前後各檢一次 <see cref="AuditTarget.Feature"/> 權限（不採用請求開始時的授權快取）。
    /// 變更後執行者不再持有該功能（例如撤掉自己的角色）時回滾並回 lockout。稽核寫進這個請求（端點宣告的動作），失敗由請求稽核記下錯誤代碼與 <c>before</c>。
    /// 等不到鎖回 <see cref="AccessControlErrors.WriteBusy"/>；與鎖外的寫入撞上時回 <see cref="AccessControlErrors.WriteConflict"/>。
    /// </summary>
    public Task<Result> ExecuteAsync(Guid actor, AuditTarget target, Func<Task<Result>> mutation, CancellationToken ct)
        => RunAsync(actor, target, item: null, mutation, ct);

    /// <summary>同上，並把變更的結果帶出來。<paramref name="mutation"/> 回傳時還沒有寫入，結果不可依賴資料庫才產生的值；需要時用無結果的版本，提交後再查。</summary>
    public async Task<Result<T>> ExecuteAsync<T>(Guid actor, AuditTarget target, Func<Task<Result<T>>> mutation, CancellationToken ct)
    {
        T? value = default;
        var outcome = await RunAsync(actor, target, item: null, async () =>
        {
            var changed = await mutation();
            if (!changed.IsSuccess) return changed.Error;
            value = changed.Value;
            return Result.Success;
        }, ct);
        return outcome.IsSuccess ? Result<T>.Ok(value!) : outcome.Error;
    }

    /// <summary>一個請求逐筆套用多筆資料（例如 AD 匯入）：每筆另寫一筆 <paramref name="item"/> 動作的稽核，失敗也各自記錄。</summary>
    public Task<Result> ExecuteItemAsync(Guid actor, AuditTarget target, AuditAction item, Func<Task<Result>> mutation, CancellationToken ct)
        => RunAsync(actor, target, item, mutation, ct);

    private async Task<Result> RunAsync(Guid actor, AuditTarget target, AuditAction? item, Func<Task<Result>> mutation, CancellationToken ct)
    {
        object? before = null;
        Error? failure;
        try { failure = await CommitAsync(); }
        catch (ExternalServiceException error) { await FailAsync(error.Error.Code); throw; }
        catch (Exception error) when (IsWriteConflict(error)) { failure = AccessControlErrors.WriteConflict; }
        if (failure is null) return Result.Success;
        await FailAsync(failure.Code);
        return failure;

        async Task<Error?> CommitAsync()
        {
            // 同範圍的寫入已由鎖排隊，不需要 Serializable：它的範圍鎖會讓不同範圍的寫入在共同讀取的權限表上互等甚至死結。
            // 隔離等級明確寫出：連線池重用的連線可能帶著別的交易留下的等級。
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            if (!await writes.TryAcquireAsync(target.Feature, ct)) return AccessControlErrors.WriteBusy;
            // 兩次權限檢查都在交易內讀 SQL，不信任請求一開始授權時的快取結果。
            access.Invalidate();
            if (!await access.HoldsAsync(actor, target.Feature, ct)) return AccessControlErrors.FeatureForbidden;
            before = await SnapshotAsync(target, ct);
            if (item is null) audit.Describe(target.Resource, target.Subject, new { resourceKey = target.Key, before });
            var outcome = await mutation();
            if (!outcome.IsSuccess) return outcome.Error;
            await db.SaveChangesAsync(ct);
            access.Invalidate();
            if (!await access.HoldsAsync(actor, target.Feature, ct)) return AccessControlErrors.Lockout;
            var after = await SnapshotAsync(target, ct);
            if (item is null) audit.Describe(details: new { after });
            else audit.Record(item, actor, target.Resource, target.Subject, new { resourceKey = target.Key, before, after });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return null;
        }

        // 交易已回滾後才會執行。先清空 ChangeTracker，避免交易中尚未提交的異動在之後的 SaveChanges 被一併寫入。
        async Task FailAsync(string code)
        {
            db.ChangeTracker.Clear(); access.Invalidate();
            if (item is null) return;
            audit.Record(item, actor, target.Resource, target.Subject, new { resourceKey = target.Key[..Math.Min(target.Key.Length, 64)], before, failureCode = code }, code);
            await db.SaveChangesAsync(ct);
        }
    }

    // 鎖只排隊行政寫入；本機登入、本人改資料等鎖外的寫入仍可能撞上同一列，由唯一索引、並行控制欄位或死結偵測擋下。
    // 這是時機問題、不是程式錯誤，重送就會成功，所以回 409 而不是 500。mutation 可能改過閉包內的狀態，這裡不自動重試。
    private static bool IsWriteConflict(Exception error) => error is DbUpdateConcurrencyException
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
/// 行政寫入的序列化範圍：預設一個模組一把，因為模組內的規則常跨多列（整棵單位樹、整份選單、同一張卡的所有期間）；不同模組互不等待。
/// 用資料庫鎖而不是程序內鎖，本機與 IIS 共用資料庫時一樣有效。必須是交易的第一個動作，說明見 <see cref="ApplicationLocks"/>。
/// </summary>
public sealed class AdministrativeWriteLock(AppDbContext db, IOptions<AccessControlOptions> options)
{
    /// <summary>取得 <paramref name="feature"/> 所屬範圍的鎖，持有到目前的交易結束；等到逾時回 <c>false</c>。</summary>
    public Task<bool> TryAcquireAsync(string feature, CancellationToken ct) => db.Database.TryAcquireApplicationLockAsync(
        $"administrative-write:{ScopeOf(feature)}", TimeSpan.FromSeconds(options.Value.WriteLockTimeoutSeconds), ct);

    /// <summary>同上，等到逾時拋 <see cref="AccessControlErrors.WriteBusy"/>：給不回傳 <see cref="Result"/> 的路徑（背景工作、回傳實體的服務）。</summary>
    public async Task AcquireAsync(string feature, CancellationToken ct)
    {
        if (!await TryAcquireAsync(feature, ct)) throw new ExternalServiceException(AccessControlErrors.WriteBusy);
    }

    private static string ScopeOf(string feature) => FeatureCatalog.Find(feature)?.Module.Id switch
    {
        // 帳號與角色都會改變「誰持有什麼功能」。分開鎖時，兩位管理員可以互相撤權，並都在對方提交前通過 lockout 檢查。
        "identity" or "access-control" => "authorization",
        var module => module ?? feature,
    };
}
