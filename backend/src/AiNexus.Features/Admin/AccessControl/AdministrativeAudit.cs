using AiNexus.Data.App;
using System.Data;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 行政變更與其前後快照共用同一個交易，變更與稽核要嘛一起成功、要嘛一起回滾。
/// 快照由擁有該資料的 slice 提供（委派），執行者由呼叫端傳入，所以這裡不依賴任何上層模組。
/// </summary>
public sealed class AdministrativeAudit(AppDbContext db, AccessService access, AdministrativeWriteLock writes, AuditTrail audit)
{
    /// <summary>
    /// 在行政寫入鎖與 Serializable 交易內執行變更，並於交易內前後各檢一次 <paramref name="feature"/> 權限（不採用請求開始時的授權快取）。
    /// <paramref name="feature"/> 是呼叫端端點所屬頁面的功能；變更後執行者不再持有它（例如撤掉自己的角色）時回滾並回 lockout。
    /// <paramref name="snapshot"/> 在變更前後各執行一次，結果寫進稽核的 <c>before</c>／<c>after</c>，JSON 形狀即稽核合約，不可任意改動。
    /// </summary>
    /// <remarks>
    /// 預設寫進這個請求的稽核（端點宣告的動作），失敗由請求稽核記下錯誤代碼與 <c>before</c>。
    /// 一個請求逐筆套用多筆資料（例如 AD 匯入）時傳 <paramref name="item"/>：每筆另寫一筆該動作的稽核，失敗也各自記錄。
    /// </remarks>
    public async Task<Result> MutateAsync(Guid actor, string feature, Guid? resource, string key, Func<CancellationToken, Task<object?>> snapshot, Func<Task<Result>> mutation, CancellationToken ct,
        Guid? subject = null, AuditAction? item = null)
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
            if (!await access.HoldsAsync(actor, feature, ct)) return AccessControlErrors.FeatureForbidden;
            before = await snapshot(ct);
            if (item is null) audit.Describe(resource, subject, new { resourceKey = key, before });
            var outcome = await mutation();
            if (!outcome.IsSuccess) return outcome.Error;
            await db.SaveChangesAsync(ct);
            access.Invalidate();
            if (!await access.HoldsAsync(actor, feature, ct)) return AccessControlErrors.Lockout;
            var after = await snapshot(ct);
            if (item is null) audit.Describe(details: new { after });
            else audit.Record(item, actor, resource, subject, new { resourceKey = key, before, after });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return null;
        }

        // 交易已回滾後才會執行。先清空 ChangeTracker，避免交易中尚未提交的異動在之後的 SaveChanges 被一併寫入。
        async Task FailAsync(string code)
        {
            db.ChangeTracker.Clear(); access.Invalidate();
            if (item is null) return;
            audit.Record(item, actor, resource, subject, new { resourceKey = key[..Math.Min(key.Length, 64)], before, failureCode = code }, code);
            await db.SaveChangesAsync(ct);
        }
    }
}
