using AiNexus.Data.App;
using System.Data;
using System.Text.Json;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 所有行政變更與其前後快照共用同一個交易，變更與稽核要嘛一起成功、要嘛一起回滾。
/// 快照由擁有該資料的 slice 提供（委派），執行者由呼叫端傳入，所以這裡不依賴任何上層模組。
/// </summary>
public sealed class AdministrativeAudit(AppDbContext db, AccessService access, AdministrativeWriteLock writes)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 在行政寫入鎖與 Serializable 交易內執行變更，並於交易內前後各檢一次 <paramref name="feature"/> 權限（不採用請求開始時的授權快取）。
    /// <paramref name="feature"/> 是呼叫端端點所屬頁面的功能；變更後執行者不再持有它（例如撤掉自己的角色）時回滾並回 lockout。
    /// <paramref name="snapshot"/> 在變更前後各執行一次，結果寫進稽核的 <c>before</c>／<c>after</c>，JSON 形狀即稽核合約，不可任意改動。
    /// 任何失敗（回傳錯誤或 <see cref="ExternalServiceException"/>）都會回滾，並以錯誤代碼寫入稽核。
    /// </summary>
    public async Task<Result> MutateAsync(Guid actor, string feature, string action, Guid? resource, string key, Func<CancellationToken, Task<object?>> snapshot, Func<Task<Result>> mutation, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        object? before = null;
        try
        {
            Error? failure;
            try { failure = await CommitAsync(); }
            catch (ExternalServiceException error) { await RecordFailureAsync(error.Error.Code); throw; }
            if (failure is null) return Result.Success;
            await RecordFailureAsync(failure.Code);
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
            var outcome = await mutation();
            if (!outcome.IsSuccess) return outcome.Error;
            await db.SaveChangesAsync(ct);
            access.Invalidate();
            if (!await access.HoldsAsync(actor, feature, ct)) return AccessControlErrors.Lockout;
            var after = await snapshot(ct);
            db.AuditEvents.Add(new()
            {
                OwnerId = actor,
                ResourceId = resource,
                Action = action,
                Result = "saved",
                DetailsJson = JsonSerializer.Serialize(new { resourceKey = key, before, after }, Json)
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return null;
        }

        // 交易已回滾後才會執行。先清空 ChangeTracker，避免交易中尚未提交的授權變更被一併寫入稽核。
        async Task RecordFailureAsync(string code)
        {
            db.ChangeTracker.Clear(); access.Invalidate();
            db.AuditEvents.Add(new()
            {
                OwnerId = actor,
                ResourceId = resource,
                Action = action,
                Result = code,
                DetailsJson = JsonSerializer.Serialize(new { resourceKey = key[..Math.Min(key.Length, 64)], before, failureCode = code }, Json)
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
