namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>最近一次掃描的結果，供監控判斷「來源資料夾讀不到」：這種失敗沒有檔案可記錄，不會出現在匯入紀錄裡。只存在記憶體，重啟後歸零。</summary>
public sealed class PunchImportState
{
    private readonly Lock gate = new();
    private DateTimeOffset? lastScanAt;
    private string? sourceError;

    public PunchScanState Current
    {
        get { lock (gate) return new(lastScanAt, sourceError); }
    }

    /// <summary>掃描成功；回傳先前是否處於讀不到來源的狀態（用來記一次恢復日誌）。</summary>
    public bool Succeeded(DateTimeOffset at)
    {
        lock (gate)
        {
            var recovered = sourceError is not null;
            (lastScanAt, sourceError) = (at, null);
            return recovered;
        }
    }

    public void Failed(DateTimeOffset at, string errorType)
    {
        lock (gate) (lastScanAt, sourceError) = (at, errorType);
    }
}

/// <summary><paramref name="SourceError"/> 是最近一次掃描讀不到來源時的例外型別名稱；null 表示讀得到（或還沒掃過）。</summary>
public sealed record PunchScanState(DateTimeOffset? LastScanAt, string? SourceError);
