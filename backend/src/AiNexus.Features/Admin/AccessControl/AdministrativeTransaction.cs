using AiNexus.Data.App;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore.Storage;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// <see cref="AdministrativeWrite.BeginAsync"/> 開始的一筆寫入。handler 改完資料後呼叫 <see cref="CommitAsync"/> 才真正寫入；沒有提交就離開（回傳錯誤、拋例外）一律回滾。
/// 要回傳成功就必須提交，漏了會由 <see cref="AuditTrail.CompleteAsync"/> 擋下，不會回成功卻沒存。
/// </summary>
/// <remarks>
/// 宣告成 <c>await using var</c> 即可，不必為了提早釋放改用區塊：<see cref="CommitAsync"/> 不論結果都已放掉交易、鎖與連線，之後的程式不佔任何資源。
/// 真正佔資源的是開始到提交之間（區塊也縮短不了），耗時的工作要放在開始之前；佔太久會記 <c>admin_write.held_too_long</c>。
/// </remarks>
public sealed class AdministrativeTransaction : IAsyncDisposable
{
    private readonly AppDbContext db;
    private readonly IDbContextTransaction transaction;
    private readonly AuditTrail audit;
    private readonly Func<CancellationToken, Task<object?>> snapshot;
    private readonly Action released;
    private bool committed;
    private bool finished;

    internal AdministrativeTransaction(AppDbContext db, IDbContextTransaction transaction, AuditTrail audit, Func<CancellationToken, Task<object?>> snapshot, Action released)
    {
        this.db = db;
        this.transaction = transaction;
        this.audit = audit;
        this.snapshot = snapshot;
        this.released = released;
    }

    /// <summary>
    /// 儲存變更、記下後值再提交；提交之後才讀得到資料庫產生的值（<c>RowVersion</c>）。只能呼叫一次。
    /// 與其他寫入撞上（版本不符、唯一索引、死結）回 <see cref="AccessControlErrors.WriteConflict"/>，不自動重試：要讓使用者看過新的資料再決定。
    /// </summary>
    public async Task<Result> CommitAsync(CancellationToken ct)
    {
        // 交易已結束時再存，變更會落在交易之外，也不會留下後值。
        ObjectDisposedException.ThrowIf(finished, this);
        try
        {
            await db.SaveChangesAsync(ct);
            audit.Describe(details: new { after = await snapshot(ct) });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            committed = true;
            return Result.Success;
        }
        catch (Exception error) when (AdministrativeWrite.IsWriteConflict(error))
        {
            return AccessControlErrors.WriteConflict;
        }
        finally
        {
            await FinishAsync();
        }
    }

    public ValueTask DisposeAsync() => FinishAsync();

    private async ValueTask FinishAsync()
    {
        if (finished) return;
        finished = true;
        await transaction.DisposeAsync();
        released();
        if (committed) return;
        // 交易已回滾：清掉未提交的異動，之後補寫失敗稽核時才不會一併寫入。
        db.ChangeTracker.Clear();
        audit.NoteRollback();
    }
}
