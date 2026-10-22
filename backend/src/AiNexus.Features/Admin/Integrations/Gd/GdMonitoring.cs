using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Integrations.Gd;

/// <summary>
/// 公文系統 GD（<c>Databases:Gd</c>）可用性的監控。未設定 idle；連不上或逾時 failing；1 小時內有查詢被拒（表、欄位或授權不符）degraded。
/// 判定來自 <see cref="GdAccess"/> 實際存取（出勤的假單、假日與組織匯入）的成敗，不另存狀態；假日回寫只依 <c>ReadOnly</c> 列出可寫入或唯讀，回寫失敗不影響判定（當場回錯誤並記日誌）。
/// </summary>
/// <remarks>
/// 只靠實際存取不夠：GD 只在有人開出勤、假日設定頁或預覽組織匯入時才被查，沒人用的時段監控會停在幾小時前的結果（GD 停了仍顯示正常、恢復了仍顯示失敗）。
/// 所以讀取時若最近 <see cref="ProbeInterval"/> 內沒有任何存取，就先做一次 <c>SELECT 1</c> 探測。探測只在有人看監控時發生（讀取是 lazy），
/// 有人在用出勤頁時完全不多打 GD；成本是每 30 秒最多一次不碰任何表的往返。未設定時不探測，直接回報未設定，不算失敗。
/// </remarks>
internal sealed class GdMonitoring(GdAccess gd, TimeProvider clock) : IMonitoringSource
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);

    // 低於監控讀取本身的 5 秒逾時：GD 卡住時回報「逾時」failing，而不是整張卡片變成「無法讀取」。
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public string Id => "gd";
    public string Name => "公文系統 GD";
    public int Order => 39;
    public TimeSpan Refresh => ProbeInterval;

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var configured = gd.Configured;
        if (configured && !(clock.GetUtcNow() - gd.Availability.LastAttemptAt < ProbeInterval)) await gd.ProbeAsync(ProbeTimeout, ct);

        var state = gd.Availability;
        var now = clock.GetUtcNow();
        var rejected = now - state.LastRejectedAt < GdAvailability.RejectedWindow;
        var (status, summary) =
            !configured ? (SubsystemStates.Idle, "未設定公文系統 GD 的連線（Databases:Gd），出勤取不到假單、出差單與假日，組織也無法從公文系統匯入。")
            : state.Outage is GdFailure.Timeout ? (SubsystemStates.Failing, "公文系統 GD 回應逾時，假單、出差單、假日與組織匯入暫時無法使用。")
            : state.Outage is not null ? (SubsystemStates.Failing, "連不上公文系統 GD，假單、出差單、假日與組織匯入暫時無法使用；請確認 GD 主機與帳號。")
            : rejected ? (SubsystemStates.Degraded, "1 小時內有 GD 查詢被拒（資料表、欄位或授權不符），部分出勤資料或組織匯入無法使用；請確認帳號的授權。")
            : state.LastSuccessAt is null ? (SubsystemStates.Idle, "尚未查詢公文系統 GD。")
            : (SubsystemStates.Healthy, "公文系統 GD 可正常查詢。");

        var connection = !configured ? "未設定" : state.Outage switch
        {
            null => state.LastSuccessAt is null ? "尚未查詢" : "正常",
            GdFailure.Timeout => "逾時",
            _ => "連不上",
        };
        var lastFailure = state.LastFailure switch
        {
            null => "無",
            GdFailure.NotConfigured => "未設定",
            GdFailure.Timeout => "逾時",
            GdFailure.Rejected => "查詢被拒",
            _ => "連不上",
        };
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Words("connection", "連線", connection, configured && state.Outage is not null ? SubsystemTones.Danger : SubsystemTones.Neutral),
            SubsystemFact.Seconds("since_success", "距上次成功查詢", Since(state.LastSuccessAt)),
            SubsystemFact.Words("last_failure", "最近一次失敗", lastFailure, rejected ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Seconds("since_failure", "距上次失敗", Since(state.LastFailureAt)),
            SubsystemFact.Words("calendar_writeback", "假日回寫", !configured ? "未設定" : gd.ReadOnly ? "唯讀" : "可寫入"),
        ];
        return new(status, summary, facts);

        double? Since(DateTimeOffset? at) => at is { } value ? Math.Max(0, Math.Round((now - value).TotalSeconds)) : null;
    }
}
