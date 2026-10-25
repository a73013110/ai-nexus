using AiNexus.Platform.Monitoring;

namespace AiNexus.Platform.Data.External;

/// <summary>
/// 外部資料庫可用性的監控，Id 是設定鍵的小寫（<c>Databases:Gd</c> → <c>gd</c>）。未設定 idle；連不上或逾時 failing；1 小時內有查詢被拒（表、欄位或授權不符）degraded。
/// 判定來自 <see cref="ExternalDatabase{TDatabase}"/> 實際存取的成敗，不另存狀態；「寫入」只依 <c>ReadOnly</c> 列出，不另外探測寫入權限。
/// </summary>
/// <remarks>
/// 只靠實際存取不夠：外部資料庫只在有人開用到它的頁面時才被查，沒人用的時段監控會停在幾小時前的結果（停了仍顯示正常、恢復了仍顯示失敗）。
/// 所以讀取時若最近 <see cref="ProbeInterval"/> 內沒有任何存取，就先做一次 <c>SELECT 1</c> 探測。探測只在有人看監控時發生（讀取是 lazy），
/// 有人在用時完全不多打對方；成本是每 30 秒最多一次不碰任何表的往返。未設定時不探測，直接回報未設定，不算失敗。
/// </remarks>
internal sealed class ExternalDatabaseMonitoring<TDatabase>(ExternalDatabase<TDatabase> database, TimeProvider clock) : IMonitoringSource
    where TDatabase : IExternalDatabaseDefinition
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);

    // 低於監控讀取本身的 5 秒逾時：對方卡住時回報「逾時」failing，而不是整張卡片變成「無法讀取」。
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public string Id => TDatabase.Name.ToLowerInvariant();
    public string Name => TDatabase.DisplayName;
    public int Order => 39;
    public TimeSpan Refresh => ProbeInterval;

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var configured = database.Configured;
        if (configured && !(clock.GetUtcNow() - database.Availability.LastAttemptAt < ProbeInterval)) await database.ProbeAsync(ProbeTimeout, ct);

        var state = database.Availability;
        var now = clock.GetUtcNow();
        var rejected = now - state.LastRejectedAt < ExternalAvailability.RejectedWindow;
        var dependents = TDatabase.Dependents;
        var (status, summary) =
            !configured ? (SubsystemStates.Idle, $"未設定連線（{DatabasesOptions.Section}:{TDatabase.Name}），{dependents}無法使用。")
            : state.Outage is ExternalFailure.Timeout ? (SubsystemStates.Failing, $"回應逾時，{dependents}暫時無法使用。")
            : state.Outage is not null ? (SubsystemStates.Failing, $"連不上，{dependents}暫時無法使用；請確認主機與帳號。")
            : rejected ? (SubsystemStates.Degraded, $"1 小時內有查詢被拒（資料表、欄位或授權不符），部分{dependents}無法使用；請確認帳號的授權。")
            : state.LastSuccessAt is null ? (SubsystemStates.Idle, "尚未查詢。")
            : (SubsystemStates.Healthy, "可正常查詢。");

        var connection = !configured ? "未設定" : state.Outage switch
        {
            null => state.LastSuccessAt is null ? "尚未查詢" : "正常",
            ExternalFailure.Timeout => "逾時",
            _ => "連不上",
        };
        var lastFailure = state.LastFailure switch
        {
            null => "無",
            ExternalFailure.NotConfigured => "未設定",
            ExternalFailure.Timeout => "逾時",
            ExternalFailure.Rejected => "查詢被拒",
            _ => "連不上",
        };
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Words("connection", "連線", connection, configured && state.Outage is not null ? SubsystemTones.Danger : SubsystemTones.Neutral),
            SubsystemFact.Seconds("since_success", "距上次成功查詢", Since(state.LastSuccessAt)),
            SubsystemFact.Words("last_failure", "最近一次失敗", lastFailure, rejected ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Seconds("since_failure", "距上次失敗", Since(state.LastFailureAt)),
            SubsystemFact.Words("writes", "寫入", !configured ? "未設定" : database.ReadOnly ? "唯讀" : "可寫入"),
        ];
        return new(status, summary, facts);

        double? Since(DateTimeOffset? at) => at is { } value ? Math.Max(0, Math.Round((now - value).TotalSeconds)) : null;
    }
}
