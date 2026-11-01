using AiNexus.Data.App;
using System.Data;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

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
/// 權限敏感的行政寫入：變更與其前後快照共用同一個交易，變更與稽核要嘛一起成功、要嘛一起回滾。
/// 快照由擁有該資料的 slice 提供（<see cref="AuditTarget"/>），執行者由呼叫端傳入，所以這裡不依賴任何上層模組。
/// </summary>
public sealed class AdministrativeWrite(AppDbContext db, AccessService access, AdministrativeWriteLock writes, AuditTrail audit)
{
    /// <summary>
    /// 在行政寫入鎖與 Serializable 交易內執行變更，並於交易內前後各檢一次 <see cref="AuditTarget.Feature"/> 權限（不採用請求開始時的授權快取）。
    /// 變更後執行者不再持有該功能（例如撤掉自己的角色）時回滾並回 lockout。稽核寫進這個請求（端點宣告的動作），失敗由請求稽核記下錯誤代碼與 <c>before</c>。
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
        await writes.Gate.WaitAsync(ct);
        object? before = null;
        try
        {
            Error? failure;
            try { failure = await CommitAsync(); }
            catch (ExternalServiceException error) { await FailAsync(error.Error.Code); throw; }
            if (failure is null) return Result.Success;
            await FailAsync(failure.Code);
            return failure;
        }
        finally { writes.Gate.Release(); }

        async Task<Error?> CommitAsync()
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // 兩次權限檢查都在交易內讀 SQL，不信任請求一開始授權時的快取結果。
            access.Invalidate();
            if (!await access.HoldsAsync(actor, target.Feature, ct)) return AccessControlErrors.FeatureForbidden;
            before = await target.Snapshot(ct);
            if (item is null) audit.Describe(target.Resource, target.Subject, new { resourceKey = target.Key, before });
            var outcome = await mutation();
            if (!outcome.IsSuccess) return outcome.Error;
            await db.SaveChangesAsync(ct);
            access.Invalidate();
            if (!await access.HoldsAsync(actor, target.Feature, ct)) return AccessControlErrors.Lockout;
            var after = await target.Snapshot(ct);
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
}

/// <summary>
/// 行政變更的串行化鎖。目前部署為單一 IIS 執行個體，程序內的 semaphore 即足夠；
/// 若改為多執行個體，這把鎖會失效，需改用資料庫層級的鎖。
/// </summary>
public sealed class AdministrativeWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
