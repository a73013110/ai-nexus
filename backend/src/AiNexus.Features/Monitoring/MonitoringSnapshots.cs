namespace AiNexus.Features.Monitoring;

/// <summary>
/// The snapshot every monitoring read and stream tick uses. A snapshot built less than <see cref="ReuseFor"/> ago for the
/// same window is shared, so N viewers cost one build per window and second, and subsystem readings come from the cache.
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
            // Disabled monitoring reads nothing, including other modules' sources.
            return recent[slot] = traffic.Snapshot(minutes, traffic.Enabled ? subsystems.Read() : []);
        }
    }
}
