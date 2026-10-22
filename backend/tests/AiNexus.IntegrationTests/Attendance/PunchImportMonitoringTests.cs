using AiNexus.Platform.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.PunchSource;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class PunchImportMonitoringTests
{
    private static readonly string Oct1 = NameOf(10, 1);

    /// <summary>以監控子系統實際使用的方式讀取：註冊的來源型別、全新的 DI scope。</summary>
    private static async Task<SubsystemReading> ReadAsync(PunchImportSession session)
    {
        var registration = session.Factory.Services.GetServices<MonitoringSourceRegistration>().Single(x => x.Type.Name == "PunchImportMonitoring");
        using var scope = session.Factory.Services.CreateScope();
        var source = (IMonitoringSource)scope.ServiceProvider.GetRequiredService(registration.Type);
        Assert.Equal("attendance-import", source.Id);
        return await source.ReadAsync(TestContext.Current.CancellationToken);
    }

    private static double? Fact(SubsystemReading reading, string id) => reading.Facts.Single(x => x.Id == id).Value;

    [Fact]
    public async Task ASuccessfulImportIsHealthyAndNothingImportedYetIsIdle()
    {
        await using var session = await PunchImportSession.CreateAsync();
        Assert.Equal(SubsystemStates.Idle, (await ReadAsync(session)).Status);

        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await session.ScanAsync();

        var reading = await ReadAsync(session);
        Assert.Equal(SubsystemStates.Healthy, reading.Status);
        Assert.Equal((0, 1), (Fact(reading, "failed_files"), Fact(reading, "imported_24h")));
    }

    [Fact]
    public async Task AFileWhoseLatestImportFailedMakesTheSubsystemUnhealthyUntilItImportsAgain()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await session.ScanAsync();
        session.Clock.Advance(TimeSpan.FromMinutes(5));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), "broken"]);
        await session.ScanAsync();

        var failing = await ReadAsync(session);

        Assert.Equal(SubsystemStates.Degraded, failing.Status);
        Assert.Equal(1, Fact(failing, "failed_files"));
        Assert.Contains("匯入失敗", failing.Summary);

        session.Clock.Advance(TimeSpan.FromMinutes(5));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 0)]);
        await session.ScanAsync();

        var recovered = await ReadAsync(session);
        Assert.Equal(SubsystemStates.Healthy, recovered.Status);
        Assert.Equal(0, Fact(recovered, "failed_files"));
    }

    [Fact]
    public async Task NoSuccessfulImportForTooLongIsUnhealthy()
    {
        await using var session = await PunchImportSession.CreateAsync(options => options.Import.StaleAfterHours = 36);
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await session.ScanAsync();

        session.Clock.Advance(TimeSpan.FromHours(35));
        Assert.Equal(SubsystemStates.Healthy, (await ReadAsync(session)).Status);

        session.Clock.Advance(TimeSpan.FromHours(2));
        var stale = await ReadAsync(session);
        Assert.Equal(SubsystemStates.Degraded, stale.Status);
        Assert.Contains("36 小時", stale.Summary);
        Assert.True(Fact(stale, "since_success") > 36 * 3600);
    }

    [Fact]
    public async Task AnUnreadableSourceFolderIsFailingAndADisabledImportIsIdle()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await session.ScanAsync();
        Directory.Delete(session.Source.Folder, recursive: true);
        await session.ScanAsync();

        Assert.Equal(SubsystemStates.Failing, (await ReadAsync(session)).Status);

        await using var disabled = await PunchImportSession.CreateAsync(options => options.Import.Enabled = false);
        Assert.Equal(SubsystemStates.Idle, (await ReadAsync(disabled)).Status);
    }
}
