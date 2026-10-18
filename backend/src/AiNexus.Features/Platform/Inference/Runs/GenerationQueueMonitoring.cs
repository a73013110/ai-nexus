using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Platform.Inference.Runs;

/// <summary>
/// 記憶體內生成佇列運行監控來源。
/// - 工作者未就緒時回報 Failing（無法受理任何生成）；
/// - 預約已達上限（拒絕新提問）或排隊數大於工作者並行數時回報 Degraded；
/// - 生成工作皆正常消化時回報 Healthy；無工作時為 Idle。
/// </summary>
internal sealed class GenerationQueueMonitoring(GenerationScheduler scheduler) : IMonitoringSource
{
    public string Id => "generation";
    public string Name => "生成佇列";
    public int Order => 10;
    public TimeSpan Refresh => TimeSpan.FromSeconds(3);

    public Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var running = scheduler.Running;
        var queued = scheduler.QueueDepth;
        var reserved = scheduler.Reserved;
        var workers = scheduler.Workers.Sum(x => x.Concurrency);
        var full = reserved >= scheduler.Capacity;
        var waiting = queued > workers;

        var (status, summary) = !scheduler.Ready
            ? (SubsystemStates.Failing, "生成工作者尚未就緒，新的提問無法開始。")
            : full
                ? (SubsystemStates.Degraded, $"佇列已滿（{reserved}／{scheduler.Capacity}），新的提問會被拒絕。")
                : waiting
                    ? (SubsystemStates.Degraded, $"{queued} 個回答排隊，超過 {workers} 個工作者能同時處理的數量。")
                    : running + queued == 0
                        ? (SubsystemStates.Idle, "目前沒有生成中的回答。")
                        : (SubsystemStates.Healthy, $"{running} 個回答生成中，{queued} 個排隊。");

        IReadOnlyList<SubsystemFact> facts =
        [
            SubsystemFact.Count("running", "生成中", running, running > 0 ? SubsystemTones.Positive : SubsystemTones.Neutral),
            SubsystemFact.Count("queued", "排隊中", queued, waiting ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Count("workers", "工作者", workers),
            SubsystemFact.Percent("capacity_used", "佇列使用率", Math.Round(100d * reserved / scheduler.Capacity, 1), full ? SubsystemTones.Danger : SubsystemTones.Neutral),
            SubsystemFact.Count("locked_conversations", "鎖定中的對話", scheduler.LockedConversations),
        ];

        return Task.FromResult(new SubsystemReading(status, summary, facts));
    }
}
