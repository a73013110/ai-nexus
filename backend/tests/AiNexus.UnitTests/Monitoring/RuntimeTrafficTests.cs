using AiNexus.Features.Identity.Users;
using AiNexus.Features.Monitoring;
using AiNexus.Features.Monitoring.Snapshots;
using AiNexus.Features.Monitoring.Traffic;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.UnitTests.Monitoring;

public sealed class RuntimeTrafficTests
{
    private static RuntimeTraffic Traffic(FakeTimeProvider clock, int capacity = 2000) => new(Options.Create(new MonitoringOptions { MaxSessions = capacity }), clock);

    private static NexusUser User(string name = "Alice") => new() { DisplayName = name, Account = "CORP\\" + name };

    private static void Heartbeat(RuntimeTraffic traffic, NexusUser user, Guid tab, string state = "active") => traffic.Presence(user, new(tab, "chat", state), "10.1.2.3", "Edge", "Windows", "ad", false);

    [Fact]
    public void PresenceCountsPeopleSeparatelyFromTabsAndExpiresWithoutReads()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)); var traffic = Traffic(clock); var user = User();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        Heartbeat(traffic, user, first); Heartbeat(traffic, user, second, "background");
        Assert.Equal(1, traffic.Snapshot(5).OnlineUsers); Assert.Equal(2, traffic.Snapshot(5).OnlineSessions);
        clock.Advance(TimeSpan.FromSeconds(70)); Heartbeat(traffic, user, first, "idle"); clock.Advance(TimeSpan.FromSeconds(21));
        var snapshot = traffic.Snapshot(5);
        Assert.Single(snapshot.Sessions); Assert.Equal("idle", snapshot.Sessions[0].State);
        traffic.Leave(user.Id, first); Assert.Empty(traffic.Snapshot(5).Sessions);
    }

    [Fact]
    public void IdentityOwnsSessionKeyAndCapacityDoesNotEvictExistingUsers()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)); var traffic = Traffic(clock, 2); var tab = Guid.NewGuid();
        var a = User(); var b = User("Bob"); var c = User("Carol");
        Heartbeat(traffic, a, tab); Heartbeat(traffic, b, tab); Heartbeat(traffic, c, tab);
        var snapshot = traffic.Snapshot(1); Assert.Equal(2, snapshot.OnlineUsers); Assert.Equal(1, snapshot.DroppedSessions);
        traffic.Leave(c.Id, tab); Assert.Equal(2, traffic.Snapshot(1).OnlineSessions);
    }

    [Fact]
    public void RollingMetricsExcludeOpenBucketAndStreamingLatencyButCountBodyBytes()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)); var traffic = Traffic(clock);
        traffic.RequestStarted(false); traffic.Transferred(120, 180); traffic.RequestFinished(null, null, "POST", "/api/v1/runs", 200, 30, 120, 180, false, false, null);
        traffic.RequestStarted(true); traffic.Transferred(0, 2000); traffic.RequestFinished(null, null, "GET", "/api/v1/runs/{id}/events", 200, 20000, 0, 2000, true, false, null);
        Assert.Equal(0, traffic.Snapshot(1).Traffic.Requests);
        clock.Advance(TimeSpan.FromSeconds(10)); var snapshot = traffic.Snapshot(1);
        Assert.Equal(2, snapshot.Traffic.Requests); Assert.Equal(.2, snapshot.Traffic.RequestsPerSecond);
        Assert.Equal(30, snapshot.Traffic.AverageMs); Assert.Equal(30, snapshot.Traffic.P95Ms);
        Assert.Equal(2180, snapshot.Traffic.SentBytes); Assert.Equal(120, snapshot.Traffic.ReceivedBytes);
        Assert.Equal(0, snapshot.InFlightRequests); Assert.Equal(0, snapshot.OpenStreams);
        clock.Advance(TimeSpan.FromSeconds(901)); Assert.Equal(0, traffic.Snapshot(15).Traffic.Requests); Assert.Empty(traffic.Snapshot(15).Activities);
    }

    [Fact]
    public void StreamingBytesAreVisibleBeforeRequestCompletes()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)); using var traffic = Traffic(clock);
        traffic.RequestStarted(true); traffic.Transferred(0, 4096); clock.Advance(TimeSpan.FromSeconds(10));
        var snapshot = traffic.Snapshot(1);
        Assert.Equal(4096, snapshot.Traffic.SentBytes); Assert.Equal(0, snapshot.Traffic.Requests);
        Assert.Equal(1, snapshot.OpenStreams);
        traffic.RequestFinished(null, null, "GET", "/api/v1/runs/{id}/events", 200, 10000, 0, 4096, true, false, null);
        clock.Advance(TimeSpan.FromSeconds(10)); Assert.Equal(4096, traffic.Snapshot(1).Traffic.SentBytes);
    }

    [Fact]
    public void TimelinePointsCarryServerErrorsAndP95AndRecentTrafficCoversOnlyTheLastMinute()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)); using var traffic = Traffic(clock);
        void Finish(int status, double ms) { traffic.RequestStarted(false); traffic.RequestFinished(null, null, "POST", "/api/v1/runs", status, ms, 0, 0, false, false, null); }
        Finish(500, 40); Finish(200, 1200);
        clock.Advance(TimeSpan.FromSeconds(10)); Finish(200, 10);
        clock.Advance(TimeSpan.FromSeconds(10));
        var snapshot = traffic.Snapshot(1);
        Assert.Equal(MonitoringSnapshot.CurrentVersion, snapshot.Version);
        Assert.Equal(2, snapshot.Timeline.Length);
        Assert.Equal((1L, 1500d), (snapshot.Timeline[0].ServerErrors, snapshot.Timeline[0].P95Ms!.Value));
        Assert.Equal((0L, 10d), (snapshot.Timeline[1].ServerErrors, snapshot.Timeline[1].P95Ms!.Value));
        Assert.Equal(3, snapshot.RecentTraffic.Requests); Assert.Equal(1, snapshot.RecentTraffic.ServerErrors);
        clock.Advance(TimeSpan.FromSeconds(70));
        snapshot = traffic.Snapshot(5);
        Assert.Equal(3, snapshot.Traffic.Requests); Assert.Equal(0, snapshot.RecentTraffic.Requests);
        Assert.Null(snapshot.Timeline[^1].P95Ms);
    }

    [Fact]
    public void ResourceTimelineAveragesCpuKeepsLastMemoryAndLongestQueuePerBucket()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)); using var traffic = Traffic(clock);
        static RuntimeResources Sample(double? cpu, long memory, long queue, double? pause) => new(cpu, memory, memory / 2, 1000, 10, 4, queue, 0, 1, 0, 0, pause, 0);
        traffic.SampleResources(Sample(10, 100, 5, 1)); clock.Advance(TimeSpan.FromSeconds(3));
        traffic.SampleResources(Sample(30, 200, 2, 3)); clock.Advance(TimeSpan.FromSeconds(3));
        traffic.SampleResources(Sample(null, 300, 1, null)); clock.Advance(TimeSpan.FromSeconds(14));
        var snapshot = traffic.Snapshot(1);
        Assert.Equal(snapshot.Timeline.Select(x => x.At), snapshot.ResourceTimeline.Select(x => x.At));
        Assert.Equal(new ResourcePoint(snapshot.Timeline[0].At, 20, 300, 150, 5, 2), snapshot.ResourceTimeline[0]);
        Assert.Equal(new ResourcePoint(snapshot.Timeline[1].At, null, null, null, null, null), snapshot.ResourceTimeline[1]);
        Assert.Equal(300, snapshot.Resources.WorkingSetBytes);
        // 環狀緩衝區的桶超過 15 分鐘保留期後會覆寫同一格；這裡確認舊取樣不會殘留在查詢結果中。
        clock.Advance(TimeSpan.FromSeconds(890)); traffic.SampleResources(Sample(50, 400, 0, 0)); clock.Advance(TimeSpan.FromSeconds(10));
        snapshot = traffic.Snapshot(15);
        Assert.Equal(90, snapshot.ResourceTimeline.Length);
        Assert.Equal(50, Assert.Single(snapshot.ResourceTimeline, x => x.CpuPercent is not null).CpuPercent);
    }

    [Fact]
    public void CancelledRequestsAreSeparateFromErrorsAndActivityRetentionIsBounded()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)); var traffic = Traffic(clock);
        for (var i = 0; i < 500; i++) {
            traffic.RequestStarted(false); traffic.RequestFinished(null, null, "POST", "/api/v1/runs", 503, 10, 0, 0, false, true, null);
        }
        clock.Advance(TimeSpan.FromSeconds(10)); var snapshot = traffic.Snapshot(1);
        Assert.Equal(500, snapshot.Traffic.Cancelled); Assert.Equal(0, snapshot.Traffic.Errors);
        Assert.Equal(0, snapshot.Traffic.ServerErrors);
        Assert.Equal(120, snapshot.Activities.Length);
    }
}
