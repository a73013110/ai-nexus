namespace AiNexus.Platform.Threading;

/// <summary>租約工作（背景工作、排程任務）共用的續租迴圈。</summary>
public static class LeaseHeartbeat
{
    /// <summary>
    /// 每 <paramref name="interval"/> 呼叫一次 <paramref name="renew"/>，直到 <paramref name="ct"/> 取消。
    /// 回傳 false 表示租約被接手或工作被要求停止，取消 <paramref name="execution"/> 後結束。
    /// 續租拋例外（資料庫暫時不可用）不取消：租約還有效，下一次再試；真的過期被接手時，下一次會回傳 false，
    /// 而過期 worker 的寫入本來就被 fencing 擋下。
    /// </summary>
    public static async Task RunAsync(
        TimeSpan interval,
        TimeProvider clock,
        Func<CancellationToken, Task<bool>> renew,
        Action<Exception> failed,
        CancellationTokenSource execution,
        CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(interval, clock, ct);
                try
                {
                    if (await renew(ct)) continue;
                    await execution.CancelAsync();
                    return;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    failed(ex);
                }
            }
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // 工作已結束才取消續租：進行中的查詢可能以 SqlException 而不是取消例外結束，收尾不能被它打斷。
        }
    }
}
