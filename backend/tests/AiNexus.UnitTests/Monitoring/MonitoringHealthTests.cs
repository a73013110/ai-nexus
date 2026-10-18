using AiNexus.Features.Monitoring.Snapshots;
using AiNexus.Platform.Monitoring;

namespace AiNexus.UnitTests.Monitoring;

public sealed class MonitoringHealthTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    private static TrafficMetrics Metrics(long requests = 0, long errors = 0, long serverErrors = 0, double? p95 = null, double? average = null) =>
        new(requests, errors, serverErrors, 0, 0, 0, requests / 60d, requests == 0 ? 0 : Math.Round(100d * errors / requests, 2), average ?? p95, p95);

    private static RuntimeResources Resources(long workingSet = 100, long? limit = null) => new(5, workingSet, 50, limit, 20, 8, 0, 0, 1, 0, 0, 0, 60);

    private static ResourcePoint Point(double? cpu = 5, long queue = 0, double? pause = 0) => new(Now, cpu, 100, 50, queue, pause);

    private static MonitoringSnapshot Snapshot(TrafficMetrics? recent = null, RuntimeResources? resources = null, ResourcePoint[]? points = null,
        DependencyTraffic[]? dependencies = null, MonitoredSubsystem[]? subsystems = null, bool enabled = true, long dropped = 0) =>
        new(MonitoringSnapshot.CurrentVersion, enabled, "test", Now, Now, 5, 3, 90, MonitoringHealth.Pending, 0, 0, 0, 0, 0, 0, dropped,
            resources ?? Resources(), points ?? [Point(), Point(), Point()], recent ?? Metrics(), recent ?? Metrics(), [], [], [], dependencies ?? [], [], subsystems ?? []);

    private static DependencyTraffic Dependency(string status, long requests, long errors) =>
        new("sql.app", "主資料庫", "database", status, 0, Now, Metrics(requests, errors));

    private static MonitoredSubsystem Subsystem(string status, DateTimeOffset? observed = null) =>
        new("jobs", "背景工作", status, "摘要", [], observed ?? Now);

    [Fact]
    public void QuietSystemWithSamplesIsHealthyAndDisabledOrEmptyMonitoringIsUnobserved()
    {
        Assert.Equal(MonitoringHealth.Healthy, MonitoringHealth.Evaluate(Snapshot()).Status);
        Assert.Equal(MonitoringHealth.Unobserved, MonitoringHealth.Evaluate(Snapshot(enabled: false, dependencies: [Dependency("failing", 9, 9)])).Status);
        var empty = Snapshot(resources: Resources(workingSet: 0), points: [], subsystems: [Subsystem(SubsystemStates.Unobserved, observed: null) with { ObservedAt = null }]);
        Assert.Equal(MonitoringHealth.Unobserved, MonitoringHealth.Evaluate(empty).Status);
    }

    [Fact]
    public void ErrorRateNeedsMinimumTrafficAndEscalatesAtTheCriticalThreshold()
    {
        Assert.DoesNotContain(MonitoringHealth.Evaluate(Snapshot(Metrics(MonitoringHealth.MinimumRequests - 1, 10))).Issues, x => x.Code == "error_rate_high");
        Assert.Empty(MonitoringHealth.Evaluate(Snapshot(Metrics(100, 5))).Issues);
        var warning = Assert.Single(MonitoringHealth.Evaluate(Snapshot(Metrics(100, 6))).Issues);
        Assert.Equal(("error_rate_high", MonitoringHealth.Warning, "api"), (warning.Code, warning.Severity, warning.Subject));
        Assert.Contains("100", warning.Detail); Assert.Contains("6%", warning.Detail);
        var critical = MonitoringHealth.Evaluate(Snapshot(Metrics(100, 26)));
        Assert.Equal(MonitoringHealth.Critical, critical.Status);
        Assert.Equal(MonitoringHealth.Critical, Assert.Single(critical.Issues).Severity);
    }

    [Fact]
    public void ServerErrorsAndSlowP95AreWarnings()
    {
        var issues = MonitoringHealth.Evaluate(Snapshot(Metrics(50, 1, 1, p95: 5000))).Issues;
        Assert.Equal(["server_errors", "latency_high"], issues.Select(x => x.Code));
        Assert.Contains("5 秒", issues[1].Detail);
        Assert.Empty(MonitoringHealth.Evaluate(Snapshot(Metrics(50, p95: 3000))).Issues);
        // 超過直方圖最後一個區間上限時，P95 會是 null（但仍有計時請求），延遲只能靠平均值判定。
        Assert.Equal("latency_high", Assert.Single(MonitoringHealth.Evaluate(Snapshot(Metrics(50, average: 700000))).Issues).Code);
        Assert.Empty(MonitoringHealth.Evaluate(Snapshot(Metrics(5, p95: 10000))).Issues);
    }

    [Fact]
    public void ResourceRulesRequireThreeSustainedBuckets()
    {
        Assert.Empty(MonitoringHealth.Evaluate(Snapshot(points: [Point(cpu: 50), Point(cpu: 95), Point(cpu: 95)])).Issues);
        Assert.Empty(MonitoringHealth.Evaluate(Snapshot(points: [Point(cpu: 95), Point(cpu: null), Point(cpu: 95)])).Issues);
        Assert.Equal("cpu_high", Assert.Single(MonitoringHealth.Evaluate(Snapshot(points: [Point(cpu: 90), Point(cpu: 95), Point(cpu: 100)])).Issues).Code);
        Assert.Equal("thread_pool_starvation", Assert.Single(MonitoringHealth.Evaluate(Snapshot(points: [Point(queue: 101), Point(queue: 150), Point(queue: 101)])).Issues).Code);
        Assert.Equal("gc_pause_high", Assert.Single(MonitoringHealth.Evaluate(Snapshot(points: [Point(pause: 11), Point(pause: 12), Point(pause: 20)])).Issues).Code);
    }

    [Fact]
    public void MemoryWarnsAboveNinetyPercentOfTheKnownLimit()
    {
        Assert.Empty(MonitoringHealth.Evaluate(Snapshot(resources: Resources(900, 1000))).Issues);
        Assert.Empty(MonitoringHealth.Evaluate(Snapshot(resources: Resources(2000, null))).Issues);
        var issue = Assert.Single(MonitoringHealth.Evaluate(Snapshot(resources: Resources(950L << 20, 1000L << 20))).Issues);
        Assert.Equal("memory_high", issue.Code); Assert.Contains("950 MB", issue.Detail);
    }

    [Fact]
    public void DependenciesSubsystemsAndDroppedSessionsAreOrderedCriticalFirst()
    {
        var health = MonitoringHealth.Evaluate(Snapshot(dropped: 2,
            dependencies: [Dependency("degraded", 10, 1), Dependency("failing", MonitoringHealth.DependencyCriticalCalls, MonitoringHealth.DependencyCriticalCalls)],
            subsystems: [Subsystem(SubsystemStates.Degraded), Subsystem(SubsystemStates.Failing), Subsystem(SubsystemStates.Unobserved), Subsystem(SubsystemStates.Idle)]));
        Assert.Equal(MonitoringHealth.Critical, health.Status);
        Assert.Equal(["dependency_failing", "subsystem_failing", "dependency_degraded", "subsystem_degraded", "subsystem_unreadable", "sessions_dropped"],
            health.Issues.Select(x => x.Code));
        Assert.Equal("sql.app", health.Issues[0].Subject);
        Assert.Equal(MonitoringHealth.Warning, MonitoringHealth.Evaluate(Snapshot(dependencies: [Dependency("failing", 1, 1)])).Issues.Single().Severity);
        Assert.Equal(MonitoringHealth.Attention, MonitoringHealth.Evaluate(Snapshot(dropped: 1)).Status);
    }
}
