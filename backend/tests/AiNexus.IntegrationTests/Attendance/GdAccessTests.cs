using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Features.Staff.Attendance.Gd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class GdAccessTests
{
    private const string UsersSql = "SELECT [ID] FROM [dbo].[Sys_User]";
    private const string MissingTableSql = "SELECT [ID] FROM [dbo].[Missing]";

    [Fact]
    public async Task AnUnreachableGdIsLoggedOnceAndTheRestorationOnceMore()
    {
        using var gd = FakeGd.Create();
        gd.AddUser("G001", "alice");
        var options = FakeGd.Unreachable();
        var log = new LogRecorder<GdAccess>();
        var access = FakeGd.AccessTo(options, log);
        var ct = TestContext.Current.CancellationToken;

        Assert.Null(await access.QueryAsync<string>(UsersSql, null, ct));
        Assert.Null(await access.QueryAsync<string>(UsersSql, null, ct));
        Assert.Equal([(3407, LogLevel.Warning)], log.Recorded);

        options[GdDatabase.Name].Name = gd.Name;
        Assert.Equal(["G001"], await access.QueryAsync<string>(UsersSql, null, ct));
        Assert.Equal(["G001"], await access.QueryAsync<string>(UsersSql, null, ct));
        Assert.Equal([(3407, LogLevel.Warning), (3408, LogLevel.Information)], log.Recorded);

        options[GdDatabase.Name].Name = "AiNexus_Test_Missing_" + Guid.NewGuid().ToString("N");
        Assert.Null(await access.QueryAsync<string>(UsersSql, null, ct));
        Assert.Equal([3407, 3408, 3407], log.Recorded.Select(x => x.Id));
    }

    [Fact]
    public async Task AGdThatIsNotConfiguredIsUnavailable()
    {
        var log = new LogRecorder<GdAccess>();

        var rows = await FakeGd.AccessTo(FakeGd.NotConfigured(), log).QueryAsync<string>(UsersSql, null, TestContext.Current.CancellationToken);

        Assert.Null(rows);
        Assert.Equal([3407], log.Recorded.Select(x => x.Id));
    }

    [Fact]
    public async Task AQueryGdRejectsIsUnavailableToo()
    {
        using var gd = FakeGd.Create();

        // GD 端改了結構（表不見了）與連不上一樣，出勤頁都只能少掉假單。
        Assert.Null(await gd.Access().QueryAsync<string>(MissingTableSql, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EveryAccessIsRecordedWithItsTimeAndReasonButNoConnectionDetails()
    {
        using var gd = FakeGd.Create();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 22, 2, 0, 0, TimeSpan.Zero));
        var options = FakeGd.Unreachable();
        var access = FakeGd.AccessTo(options, clock: clock);
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(GdAvailability.Unknown, access.Availability);

        await access.QueryAsync<string>(UsersSql, null, ct);
        var down = access.Availability;
        Assert.Equal((GdFailure.Unavailable, GdFailure.Unavailable, clock.GetUtcNow()), (down.Outage, down.LastFailure, down.LastFailureAt));
        Assert.Null(down.LastSuccessAt);

        clock.Advance(TimeSpan.FromMinutes(1));
        options[GdDatabase.Name].Name = gd.Name;
        await access.QueryAsync<string>(UsersSql, null, ct);
        var up = access.Availability;
        Assert.Null(up.Outage);
        Assert.Equal(clock.GetUtcNow(), up.LastSuccessAt);
        Assert.Equal(clock.GetUtcNow(), up.LastAttemptAt);
        // 最近一次失敗留著給監控顯示，但已不算中斷。
        Assert.Equal(GdFailure.Unavailable, up.LastFailure);
    }

    [Fact]
    public async Task ARejectedQueryIsLoggedOncePerEpisodeAndIsNotAnOutage()
    {
        using var gd = FakeGd.Create();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 22, 2, 0, 0, TimeSpan.Zero));
        var log = new LogRecorder<GdAccess>();
        var access = gd.Access(log, clock);
        var ct = TestContext.Current.CancellationToken;

        Assert.Null(await access.QueryAsync<string>(MissingTableSql, null, ct));
        clock.Advance(TimeSpan.FromMinutes(50));
        Assert.Null(await access.QueryAsync<string>(MissingTableSql, null, ct));
        // 其他查詢照常成功：連線沒有中斷，所以不記恢復。
        Assert.NotNull(await access.QueryAsync<string>(UsersSql, null, ct));
        Assert.Equal([(3411, LogLevel.Warning)], log.Recorded);
        Assert.Null(access.Availability.Outage);
        Assert.Equal((GdFailure.Rejected, clock.GetUtcNow()), (access.Availability.LastFailure, access.Availability.LastRejectedAt));

        // 距上一次被拒超過一小時才算新的一段。
        clock.Advance(GdAvailability.RejectedWindow);
        Assert.Null(await access.QueryAsync<string>(MissingTableSql, null, ct));
        Assert.Equal([3411, 3411], log.Recorded.Select(x => x.Id));
    }

    [Fact]
    public async Task AProbeDetectsAnOutageAndTheRecoveryWithoutAnyRealQuery()
    {
        using var gd = FakeGd.Create();
        var options = FakeGd.Unreachable();
        var log = new LogRecorder<GdAccess>();
        var access = FakeGd.AccessTo(options, log);
        var ct = TestContext.Current.CancellationToken;

        await access.ProbeAsync(TimeSpan.FromSeconds(3), ct);
        Assert.Equal(GdFailure.Unavailable, access.Availability.Outage);

        options[GdDatabase.Name].Name = gd.Name;
        await access.ProbeAsync(TimeSpan.FromSeconds(3), ct);
        Assert.Null(access.Availability.Outage);
        Assert.Equal([(3407, LogLevel.Warning), (3408, LogLevel.Information)], log.Recorded);
    }

    [Fact]
    public async Task ACancelledQueryIsNotTreatedAsUnavailable()
    {
        using var gd = FakeGd.Create();
        var log = new LogRecorder<GdAccess>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gd.Access(log).QueryAsync<string>(UsersSql, null, cancelled.Token));

        Assert.Empty(log.Recorded);
    }

    [Fact]
    public async Task LongKeyListsAreSentInBatchesAndTheRowsAreConcatenated()
    {
        using var gd = FakeGd.Create();
        gd.AddUser("U0001", "a");
        gd.AddUser("U0700", "b");
        gd.AddUser("U1199", "c");
        var keys = Enumerable.Range(1, 1200).Select(x => $"U{x:D4}").ToList();

        var rows = await gd.Access().QueryInAsync<string>("SELECT [ID] FROM [dbo].[Sys_User] WHERE [ID] IN @Ids", keys, batch => new { Ids = batch }, TestContext.Current.CancellationToken);

        Assert.NotNull(rows);
        Assert.Equal(["U0001", "U0700", "U1199"], rows.Order(StringComparer.Ordinal));
    }
}
