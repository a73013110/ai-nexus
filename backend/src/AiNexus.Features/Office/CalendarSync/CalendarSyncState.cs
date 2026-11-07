using System.Threading.Channels;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Office.CalendarSync;

/// <summary>手動同步的請求：操作者與是否確認被安全閥擋下的移除。</summary>
public sealed record CalendarSyncRequest(Guid RequestedBy, bool ConfirmRemovals);

/// <summary>最近一次檢查（每一輪，不論有沒有異動）的結果；只存在記憶體，重啟後歸零。</summary>
public sealed record CalendarSyncCheck(DateTimeOffset At, string Status, string? ErrorCode, int HeldCount);

/// <summary>
/// 同步的記憶體狀態：最近一次檢查、是否正在執行，以及管理頁「立即同步」排進來的請求。沒有異動的排程輪不寫執行紀錄，
/// 「最後檢查時間」只能放這裡；監控與管理頁都讀它。與其他背景工作的狀態一樣只在記憶體，重啟後歸零，所以註冊為單例。
/// </summary>
[Singleton]
public sealed class CalendarSyncState
{
    private readonly Channel<CalendarSyncRequest> requests = Channel.CreateBounded<CalendarSyncRequest>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private CalendarSyncCheck? lastCheck;
    private int running;

    public CalendarSyncCheck? LastCheck => Volatile.Read(ref lastCheck);

    public bool Running => Volatile.Read(ref running) == 1;

    /// <summary>
    /// 排進一次手動同步，背景工作立刻醒來執行。已經有一個在排隊時以新的取代（只保留最後一次的操作者與確認旗標），連按幾次只跑一輪。
    /// </summary>
    public void Request(CalendarSyncRequest request) => requests.Writer.TryWrite(request);

    internal ChannelReader<CalendarSyncRequest> Requests => requests.Reader;

    internal void Started() => Volatile.Write(ref running, 1);

    /// <summary>記下這一輪的結果；回傳 true 表示由失敗轉為正常或反過來，呼叫端據此只在轉換時記日誌。</summary>
    internal bool Finished(CalendarSyncCheck check)
    {
        var previous = Interlocked.Exchange(ref lastCheck, check);
        Volatile.Write(ref running, 0);
        return previous is not null && (previous.ErrorCode is null) != (check.ErrorCode is null);
    }
}
