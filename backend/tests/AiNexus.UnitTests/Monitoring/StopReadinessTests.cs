using AiNexus.Features.Admin.Monitoring.Snapshots;
using AiNexus.Platform.Monitoring;
using static AiNexus.UnitTests.Monitoring.MonitoringHealthTests;

namespace AiNexus.UnitTests.Monitoring;

public sealed class StopReadinessTests
{
    private static MonitoredSubsystem Subsystem(string id, StopImpact? stop, string status = SubsystemStates.Healthy) =>
        new(id, id, status, null, [], DateTimeOffset.UnixEpoch, stop);

    [Fact]
    public void SumsEveryReportedImpactAndOnlyInterruptionsBlockStopping()
    {
        var resuming = Snapshot(subsystems: [Subsystem("jobs", new(0, 2)), Subsystem("mail", new(0, 3)), Subsystem("storage", null)]);
        Assert.Equal(new StopSummary(StopReadiness.Clear, 0, 5), StopReadiness.Evaluate(resuming));

        var generating = Snapshot(subsystems: [Subsystem("generation", new(2, 0)), Subsystem("jobs", new(0, 1))]);
        Assert.Equal(new StopSummary(StopReadiness.Interrupts, 2, 1), StopReadiness.Evaluate(generating));
    }

    [Fact]
    public void UnreadableOrMissingSubsystemsMakeAQuietSnapshotUnknown()
    {
        var blind = Snapshot(subsystems: [Subsystem("generation", new(0, 0)), Subsystem("jobs", null, SubsystemStates.Unobserved)]);
        Assert.Equal(StopReadiness.Unknown, StopReadiness.Evaluate(blind).Status);
        // 已知會中斷時直接回答，不因其他子系統讀不到而降成無法確定。
        var interrupting = Snapshot(subsystems: [Subsystem("generation", new(1, 0)), Subsystem("jobs", null, SubsystemStates.Unobserved)]);
        Assert.Equal(StopReadiness.Interrupts, StopReadiness.Evaluate(interrupting).Status);
        Assert.Equal(StopReadiness.Pending, StopReadiness.Evaluate(Snapshot()));
        Assert.Equal(StopReadiness.Pending, StopReadiness.Evaluate(Snapshot(subsystems: [Subsystem("jobs", new(0, 1))], enabled: false)));
    }
}
