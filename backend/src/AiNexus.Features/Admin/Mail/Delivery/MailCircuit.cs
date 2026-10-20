namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// 傳輸層斷路器：認證、TLS、relay 拒絕或連續 <see cref="ConnectionFailureLimit"/> 次連線失敗時暫停認領 <see cref="OpenFor"/>，
/// 避免整批信在故障期間耗盡重試。狀態只在記憶體（單一 dispatcher）；重啟後 <see cref="MailDispatcher"/> 會把因故障延後的信提前，等於重試一次。
/// </summary>
public sealed class MailCircuit(TimeProvider clock)
{
    public static readonly TimeSpan OpenFor = TimeSpan.FromMinutes(5);
    public const int ConnectionFailureLimit = 3;

    private readonly Lock gate = new();
    private int connectionFailures;
    private bool tripped;

    /// <summary>暫停到這個時間；null 表示未暫停。</summary>
    public DateTimeOffset? OpenUntil { get; private set; }

    /// <summary>最近一次開啟的原因代碼（管理頁與監控顯示）。</summary>
    public string? LastErrorCode { get; private set; }

    public bool IsOpen => OpenUntil is { } until && clock.GetUtcNow() < until;

    /// <summary>開啟斷路器；回傳 true 表示這次是從正常轉為暫停（只記一次 Error 日誌）。</summary>
    public bool Open(string errorCode)
    {
        lock (gate)
        {
            var first = !tripped;
            tripped = true;
            connectionFailures = 0;
            OpenUntil = clock.GetUtcNow() + OpenFor;
            LastErrorCode = errorCode;
            return first;
        }
    }

    /// <summary>記一次連線層失敗；達到上限時開啟並回傳 true（且是首次開啟）。</summary>
    public bool ConnectionFailed(string errorCode)
    {
        lock (gate)
        {
            if (++connectionFailures < ConnectionFailureLimit) return false;
        }
        return Open(errorCode);
    }

    /// <summary>
    /// 管理者要求立即重試：結束暫停，但保留「曾經故障」——下一封寄出時照常記恢復並提前延後的信，再失敗時也不重複記 Error 日誌。
    /// </summary>
    public void Resume()
    {
        lock (gate)
        {
            connectionFailures = 0;
            OpenUntil = null;
        }
    }

    /// <summary>寄出成功；回傳 true 表示先前曾暫停、現在恢復（記一次恢復日誌）。</summary>
    public bool Succeeded()
    {
        lock (gate)
        {
            connectionFailures = 0;
            var restored = tripped;
            tripped = false;
            OpenUntil = null;
            return restored;
        }
    }
}
