using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data;
using AiNexus.Platform.Data.External;
using AiNexus.Platform.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Platform;

/// <summary>外部資料庫的監控卡片，以假 GD（<c>gd</c>）為對象。</summary>
public sealed class ExternalDatabaseMonitoringTests
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);

    /// <summary>一個 host 與一個假 GD；<see cref="Connection"/> 與 host 用的是同一個設定物件，改它的 Name 就能讓 GD 中途失效或恢復。</summary>
    private sealed class Session : IAsyncDisposable
    {
        public required FakeGd Gd { get; init; }
        public required AppFactory Factory { get; init; }
        public required FakeTimeProvider Clock { get; init; }
        public DatabaseOptions Connection => Gd.Databases[GdDatabase.Name];

        public static Session Create(bool configured = true)
        {
            var gd = FakeGd.Create();
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 22, 10, 0, 0, AttendanceApi.Taipei));
            var factory = new AppFactory(clock: clock, services: services => services.PostConfigure<DatabasesOptions>(options =>
            {
                if (configured) options[GdDatabase.Name] = gd.Databases[GdDatabase.Name];
            }));
            return new() { Gd = gd, Factory = factory, Clock = clock };
        }

        public ExternalDatabase<GdDatabase> Access => Factory.Services.GetRequiredService<ExternalDatabase<GdDatabase>>();

        public void BreakGd() => Connection.Name = "AiNexus_Test_Missing_" + Guid.NewGuid().ToString("N");

        public void RestoreGd() => Connection.Name = Gd.Name;

        /// <summary>以監控子系統實際使用的方式讀取：註冊的來源型別、全新的 DI scope。</summary>
        public async Task<SubsystemReading> ReadAsync()
        {
            var registration = Factory.Services.GetServices<MonitoringSourceRegistration>().Single(x => x.Type.GenericTypeArguments is [var database] && database == typeof(GdDatabase));
            using var scope = Factory.Services.CreateScope();
            var source = (IMonitoringSource)scope.ServiceProvider.GetRequiredService(registration.Type);
            Assert.Equal(("gd", "公文系統"), (source.Id, source.Name));
            return await source.ReadAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            RestoreGd();
            await Factory.DisposeAsync();
            Gd.Dispose();
        }
    }

    private static string? Text(SubsystemReading reading, string id) => reading.Facts.Single(x => x.Id == id).Text;

    [Fact]
    public async Task AReachableGdIsHealthyWithoutWaitingForARealQuery()
    {
        await using var session = Session.Create();

        var reading = await session.ReadAsync();

        Assert.Equal(SubsystemStates.Healthy, reading.Status);
        Assert.Equal(("正常", "無"), (Text(reading, "connection"), Text(reading, "last_failure")));
        Assert.Equal(session.Clock.GetUtcNow(), session.Access.Availability.LastSuccessAt);
    }

    [Fact]
    public async Task AFailedQueryTurnsFailingAndAProbeLaterSeesTheRecovery()
    {
        await using var session = Session.Create();
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(SubsystemStates.Healthy, (await session.ReadAsync()).Status);

        session.BreakGd();
        // 剛探測過，不會再探：GD 已經壞了，監控仍是上一次的結果。
        Assert.Equal(SubsystemStates.Healthy, (await session.ReadAsync()).Status);
        // 出勤頁的實際查詢失敗，監控下一次讀取就轉為 failing，不必等探測。
        Assert.Null(await session.Access.QueryAsync<string>("SELECT [ID] FROM [dbo].[Sys_User]", null, ct));
        var failing = await session.ReadAsync();
        Assert.Equal(SubsystemStates.Failing, failing.Status);
        Assert.Equal(("連不上", "連不上"), (Text(failing, "connection"), Text(failing, "last_failure")));
        Assert.Contains("連不上，假單、出差單、假日與組織匯入暫時無法使用", failing.Summary);

        session.RestoreGd();
        Assert.Equal(SubsystemStates.Failing, (await session.ReadAsync()).Status);
        // 沒有人再查 GD，過了探測間隔由探測發現恢復。
        session.Clock.Advance(ProbeInterval);
        var recovered = await session.ReadAsync();
        Assert.Equal(SubsystemStates.Healthy, recovered.Status);
        Assert.Equal(0, recovered.Facts.Single(x => x.Id == "since_success").Value);
    }

    [Fact]
    public async Task AnOutageNobodyQueriedIsFoundByTheNextProbe()
    {
        await using var session = Session.Create();
        Assert.Equal(SubsystemStates.Healthy, (await session.ReadAsync()).Status);

        session.BreakGd();
        session.Clock.Advance(ProbeInterval);

        Assert.Equal(SubsystemStates.Failing, (await session.ReadAsync()).Status);
    }

    [Fact]
    public async Task ARejectedQueryIsDegradedForAnHourEvenThoughProbesSucceed()
    {
        await using var session = Session.Create();
        Assert.Null(await session.Access.QueryAsync<string>("SELECT [ID] FROM [dbo].[Missing]", null, TestContext.Current.CancellationToken));

        var degraded = await session.ReadAsync();
        Assert.Equal(SubsystemStates.Degraded, degraded.Status);
        Assert.Equal("查詢被拒", Text(degraded, "last_failure"));

        session.Clock.Advance(ProbeInterval);
        Assert.Equal(SubsystemStates.Degraded, (await session.ReadAsync()).Status);
        Assert.Equal(session.Clock.GetUtcNow(), session.Access.Availability.LastSuccessAt);

        session.Clock.Advance(ExternalAvailability.RejectedWindow);
        Assert.Equal(SubsystemStates.Healthy, (await session.ReadAsync()).Status);
    }

    [Theory]
    [InlineData(false, "可寫入")]
    [InlineData(true, "唯讀")]
    public async Task TheWritesFactFollowsReadOnly(bool readOnly, string expected)
    {
        await using var session = Session.Create();
        session.Connection.ReadOnly = readOnly;

        var reading = await session.ReadAsync();

        Assert.Equal(SubsystemStates.Healthy, reading.Status);
        Assert.Equal(expected, Text(reading, "writes"));
    }

    [Fact]
    public async Task AGdThatIsNotConfiguredIsIdleAndNeverProbed()
    {
        await using var session = Session.Create(configured: false);

        var reading = await session.ReadAsync();

        Assert.Equal(SubsystemStates.Idle, reading.Status);
        Assert.Contains("未設定連線（Databases:Gd）", reading.Summary);
        Assert.Equal(("未設定", "未設定"), (Text(reading, "connection"), Text(reading, "writes")));
        Assert.Null(session.Access.Availability.LastAttemptAt);
    }
}
