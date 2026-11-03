using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AiNexus.Platform.Data;

/// <summary>SQL Server 應用程式鎖：互斥的對象是一個名稱而不是資料列，所以能保護「整棵樹」「整份清單」這類沒有單一資料列可鎖的規則，也跨程序有效。</summary>
public static class ApplicationLocks
{
    // 等鎖之外，命令本身只需要一次往返；這段只留給網路或伺服器短暫變慢。
    private static readonly TimeSpan CommandMargin = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 在目前的交易取得 <paramref name="resource"/> 的互斥鎖，交易提交或回復時自動釋放；等到逾時回 <c>false</c>。
    /// 要放在交易的第一個動作：先持有資料列鎖再等這把鎖，可能與持有這把鎖、正要改那些資料列的交易互等成死結。
    /// <paramref name="shared"/> 為 true 時取共用鎖：持有者彼此不等待，只與互斥鎖互斥，用在「多個寫入者可以同時做、但要與某個動作隔開」的規則。
    /// </summary>
    public static async Task<bool> TryAcquireApplicationLockAsync(this DatabaseFacade database, string resource, TimeSpan timeout, CancellationToken ct, bool shared = false)
    {
        var outcome = new SqlParameter("@outcome", SqlDbType.Int) { Direction = ParameterDirection.Output };
        // 命令逾時不可比等鎖的上限短，否則先到的是 SQL 例外，呼叫端分不清「等不到鎖」與「資料庫出錯」。
        var commandTimeout = database.GetCommandTimeout();
        database.SetCommandTimeout(timeout + CommandMargin);
        try
        {
            await database.ExecuteSqlRawAsync(
                "EXEC @outcome = sys.sp_getapplock @Resource = @resource, @LockMode = @mode, @LockOwner = 'Transaction', @LockTimeout = @timeout",
                [outcome, new SqlParameter("@resource", resource), new SqlParameter("@mode", shared ? "Shared" : "Exclusive"), new SqlParameter("@timeout", (int)timeout.TotalMilliseconds)], ct);
        }
        finally
        {
            database.SetCommandTimeout(commandTimeout);
        }
        // 0 是立刻取得、1 是等候後取得；負值是逾時、取消或死結犧牲者，都沒有拿到鎖。
        return (int)outcome.Value >= 0;
    }
}
