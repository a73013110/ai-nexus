using AiNexus.Features.Monitoring.Traffic;
namespace AiNexus.Features.Monitoring.Snapshots;

/// <summary>
/// 所有監控讀取與串流推送共用的快照。同一時間窗內，距上次建置不到 <see cref="ReuseFor"/> 者直接共用，因此 N 位檢視者每個時間窗每秒只建置一次，子系統讀數也來自快取。
/// </summary>
public sealed class MonitoringSnapshots(RuntimeTraffic traffic, SubsystemReadings subsystems, TimeProvider clock)
{
    public static readonly TimeSpan ReuseFor = TimeSpan.FromSeconds(1);
    private readonly Lock gate = new();
    private readonly MonitoringSnapshot?[] recent = new MonitoringSnapshot?[3];

    public MonitoringSnapshot Read(int minutes)
    {
        var slot = minutes switch { 1 => 0, 5 => 1, 15 => 2, _ => throw new ArgumentOutOfRangeException(nameof(minutes)) };
        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (recent[slot] is { } cached && now >= cached.At && now - cached.At < ReuseFor) return cached;
            // 監控停用時不讀取任何子系統，避免停用後仍向其他模組的資料來源查詢。
            return recent[slot] = traffic.Snapshot(minutes, traffic.Enabled ? subsystems.Read() : []);
        }
    }
}
