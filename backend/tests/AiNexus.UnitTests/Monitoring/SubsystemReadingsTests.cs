using AiNexus.Features.Admin.Monitoring;
using AiNexus.Features.Admin.Monitoring.Snapshots;
using AiNexus.Features.Admin.Monitoring.Traffic;
using AiNexus.Platform.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.UnitTests.Monitoring;

public sealed class SubsystemReadingsTests
{
    private static readonly SubsystemReading Ready = new(SubsystemStates.Healthy, "正常", [SubsystemFact.Count("items", "項目", 3)]);

    private static (SubsystemReadings Readings, Probe Probe, FakeTimeProvider Clock) Create()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero));
        var probe = new Probe();
        var services = new ServiceCollection().AddSingleton(probe).AddMonitoringSource<ProbeSource>().AddMonitoringSource<QuietSource>().BuildServiceProvider();
        var readings = new SubsystemReadings(services.GetRequiredService<IServiceScopeFactory>(), services.GetServices<MonitoringSourceRegistration>(), clock,
            NullLogger<SubsystemReadings>.Instance);
        return (readings, probe, clock);
    }

    [Fact]
    public async Task ReadReturnsTheCacheAtOnceAndStartsOneReadPerStaleSource()
    {
        var (readings, probe, clock) = Create();
        var first = readings.Read();
        Assert.Equal(["quiet", "probe"], first.Select(x => x.Id));
        Assert.Equal((SubsystemStates.Unobserved, (DateTimeOffset?)null), (first[1].Status, first[1].ObservedAt));
        readings.Read(); readings.Read();
        probe.Next.SetResult(Ready);
        await readings.Settled;
        Assert.Equal(1, probe.Reads);
        var read = readings.Read()[1];
        Assert.Equal((SubsystemStates.Healthy, "正常", clock.GetUtcNow()), (read.Status, read.Summary, read.ObservedAt!.Value));
        Assert.Equal("items", Assert.Single(read.Facts).Id);
        await readings.Settled; Assert.Equal(1, probe.Reads);
        clock.Advance(ProbeSource.Interval);
        Assert.Equal(read, readings.Read()[1]);
        await readings.Settled; Assert.Equal(2, probe.Reads);
    }

    [Fact]
    public async Task FailingSourceIsUnobservedUntilItsNextRefresh()
    {
        var (readings, probe, clock) = Create();
        probe.Fail = new InvalidOperationException("private");
        readings.Read(); await readings.Settled;
        var failed = readings.Read()[1];
        Assert.Equal((SubsystemStates.Unobserved, SubsystemReadings.UnreadableSummary), (failed.Status, failed.Summary));
        Assert.Empty(failed.Facts); Assert.NotNull(failed.ObservedAt);
        probe.Fail = null; probe.Next.SetResult(Ready);
        await readings.Settled; Assert.Equal(1, probe.Reads);
        clock.Advance(ProbeSource.Interval); readings.Read(); await readings.Settled;
        Assert.Equal(SubsystemStates.Healthy, readings.Read()[1].Status);
    }

    [Fact]
    public async Task SlowSourceTimesOutWithoutBlockingSnapshots()
    {
        var (readings, probe, clock) = Create();
        Assert.Equal(SubsystemStates.Unobserved, readings.Read()[1].Status);
        await probe.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(SubsystemReadings.Timeout);
        await readings.Settled;
        var timedOut = readings.Read()[1];
        Assert.Equal((SubsystemStates.Unobserved, SubsystemReadings.UnreadableSummary), (timedOut.Status, timedOut.Summary));
    }

    [Fact]
    public void SnapshotsAreSharedForOneSecondPerWindowAndSkipSourcesWhenDisabled()
    {
        var (readings, probe, clock) = Create();
        using var traffic = new RuntimeTraffic(Options.Create(new MonitoringOptions()), clock);
        var snapshots = new MonitoringSnapshots(traffic, readings, clock);
        var first = snapshots.Read(5);
        Assert.Same(first, snapshots.Read(5)); Assert.NotSame(first, snapshots.Read(1));
        Assert.Equal(["quiet", "probe"], first.Subsystems.Select(x => x.Id));
        clock.Advance(MonitoringSnapshots.ReuseFor);
        Assert.NotSame(first, snapshots.Read(5));
        using var disabled = new RuntimeTraffic(Options.Create(new MonitoringOptions { Enabled = false }), clock);
        var (idle, untouched, _) = Create();
        var snapshot = new MonitoringSnapshots(disabled, idle, clock).Read(5);
        Assert.Empty(snapshot.Subsystems); Assert.Equal(MonitoringHealth.Unobserved, snapshot.Health.Status);
        Assert.False(untouched.Started.Task.IsCompleted);
        probe.Next.SetResult(Ready);
    }

    public sealed class Probe
    {
        public int Reads;
        public Exception? Fail;
        public TaskCompletionSource<SubsystemReading> Next { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ProbeSource(Probe probe) : IMonitoringSource
    {
        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
        public string Id => "probe";
        public string Name => "探針";
        public int Order => 2;
        public TimeSpan Refresh => Interval;

        public Task<SubsystemReading> ReadAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref probe.Reads);
            probe.Started.TrySetResult();
            if (probe.Fail is { } error) throw error;
            return probe.Next.Task.WaitAsync(ct);
        }
    }

    private sealed class QuietSource : IMonitoringSource
    {
        public string Id => "quiet";
        public string Name => "安靜";
        public int Order => 1;
        public TimeSpan Refresh => TimeSpan.FromMinutes(5);
        public Task<SubsystemReading> ReadAsync(CancellationToken ct) => Task.FromResult(new SubsystemReading(SubsystemStates.Idle, null, []));
    }
}
