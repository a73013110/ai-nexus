using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Billing.Reports;

/// <summary>
/// 今日模型用量的執行期監控（以伺服器本地日計算），資料來自 <see cref="UsageReports"/> 的會計查詢，
/// 涵蓋所有擁有者的聊天執行與模型工作。失敗次數至少達 <see cref="MinimumFailures"/>，且占今日呼叫超過
/// <see cref="FailurePercent"/>% 時為 degraded。
/// </summary>
internal sealed class UsageMonitoring(UsageReports reports, TimeProvider clock) : IMonitoringSource
{
    public const int MinimumFailures = 5;
    public const double FailurePercent = 20;

    public string Id => "usage-today";
    public string Name => "今日用量";
    public int Order => 60;
    public TimeSpan Refresh => TimeSpan.FromSeconds(60);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var midnight = clock.GetLocalNow().Date;
        var today = new DateTimeOffset(midnight, clock.LocalTimeZone.GetUtcOffset(midnight));
        var usage = await reports.AllSinceAsync(today, ct);
        var people = await reports.ActiveOwnersSinceAsync(today, ct);
        var failing = usage.Failed >= MinimumFailures && usage.Failed * 100d / usage.Requests > FailurePercent;
        var (status, summary) =
            failing ? (SubsystemStates.Degraded, $"今日 {usage.Requests} 次模型呼叫中有 {usage.Failed} 次失敗。")
            : usage.Requests == 0 ? (SubsystemStates.Idle, "今日尚無模型呼叫。")
            : (SubsystemStates.Healthy, $"今日 {people} 位使用者共 {usage.Requests} 次模型呼叫。");
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Count("requests", "模型呼叫", usage.Requests),
            SubsystemFact.Count("failed", "失敗", usage.Failed,
                failing ? SubsystemTones.Danger : usage.Failed > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Tokens("tokens", "Token", usage.InputTokens + usage.OutputTokens),
            SubsystemFact.Count("active_users", "使用者", people),
        ];
        return new(status, summary, facts);
    }
}
