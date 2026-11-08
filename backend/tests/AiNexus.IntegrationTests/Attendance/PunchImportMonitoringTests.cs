using AiNexus.Data.App;
using AiNexus.Data.App.Scheduling;
using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Office.Attendance.Imports;
using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data;
using AiNexus.Platform.Monitoring;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>排程引擎在測試裡不啟動，直接寫一筆它會留下的執行紀錄。</summary>
    private static async Task RecordRunAsync(PunchImportSession session, string status, string? errorCode)
    {
        using var scope = session.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var at = session.Clock.GetUtcNow();
        if (!await db.ScheduledTasks.AnyAsync(x => x.Key == PunchImportTask.Definition.Key, TestContext.Current.CancellationToken))
            db.ScheduledTasks.Add(new() { Key = PunchImportTask.Definition.Key, UpdatedAt = at });
        db.ScheduledTaskRuns.Add(new()
        {
            TaskKey = PunchImportTask.Definition.Key, TriggerKind = ScheduledTaskTriggers.Scheduled, Host = "test",
            StartedAt = at, FinishedAt = at, Status = status, ErrorCode = errorCode,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>測試時鐘從 2026-10-22（週四）10:00 開始；HAMS 沒有新刷卡就不寫檔，週末沒有匯入是正常的。</summary>
    [Fact]
    public async Task AWeekendWithoutImportsIsHealthyButAWholeMissedWorkdayIsNot()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Clock.Advance(TimeSpan.FromDays(1));
        session.Source.Write(NameOf(10, 23), [Line("A001", 10, 23, 8, 0)]);
        await session.ScanAsync();

        // 週一 10:00：週六、週日沒有匯入，GD 沒設定時以週一到週五為工作日；今天的檔中午才轉出，還不算。
        session.Clock.Advance(TimeSpan.FromDays(3));
        Assert.Equal(SubsystemStates.Healthy, (await ReadAsync(session)).Status);

        session.Clock.Advance(TimeSpan.FromDays(1));
        var stale = await ReadAsync(session);
        Assert.Equal(SubsystemStates.Degraded, stale.Status);
        Assert.Contains("2026-10-26", stale.Summary);
        Assert.True(Fact(stale, "since_success") > 3 * 86400);
    }

    [Fact]
    public async Task TheGdCalendarDecidesHolidaysAndMakeUpWorkdays()
    {
        using var gd = FakeGd.Create();
        await using var session = await PunchImportSession.CreateAsync(services: services =>
            services.PostConfigure<DatabasesOptions>(options => options[GdDatabase.Name] = gd.Databases[GdDatabase.Name]));
        session.Clock.Advance(TimeSpan.FromDays(1));
        session.Source.Write(NameOf(10, 23), [Line("A001", 10, 23, 8, 0)]);
        await session.ScanAsync();
        gd.AddDay(new(2026, 10, 24), morningOff: true, afternoonOff: true);
        gd.AddDay(new(2026, 10, 25), morningOff: true, afternoonOff: true);
        gd.AddDay(new(2026, 10, 26), morningOff: true, afternoonOff: true, remark: "補假");
        session.Clock.Advance(TimeSpan.FromDays(4));

        // 週二 10:00：週一是 GD 的假日，不算漏掉。
        Assert.Equal(SubsystemStates.Healthy, (await ReadAsync(session)).Status);

        // 週六改成補班日（只放下午也是工作日）：那天整天沒有匯入。
        gd.RemoveDay(new(2026, 10, 24));
        gd.AddDay(new(2026, 10, 24), afternoonOff: true, remark: "補班");
        var stale = await ReadAsync(session);
        Assert.Equal(SubsystemStates.Degraded, stale.Status);
        Assert.Contains("2026-10-24", stale.Summary);
    }

    [Fact]
    public async Task AnUnreadableSourceFolderIsFailingUntilAScanSucceedsAndADisabledImportIsIdle()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await session.ScanAsync();
        // 來源讀不到沒有匯入列可看，監控讀排程任務最近一輪的結果，所以不論哪個程序掃描都一樣。
        await RecordRunAsync(session, ScheduledTaskRunStatuses.Failed, AttendanceErrors.PunchSourceUnavailable.Code);

        var down = await ReadAsync(session);
        Assert.Equal(SubsystemStates.Failing, down.Status);
        Assert.Equal("讀不到", down.Facts.Single(x => x.Id == "source").Text);

        session.Clock.Advance(TimeSpan.FromMinutes(5));
        await RecordRunAsync(session, ScheduledTaskRunStatuses.Idle, null);
        Assert.Equal(SubsystemStates.Healthy, (await ReadAsync(session)).Status);

        await using var disabled = await PunchImportSession.CreateAsync(options => options.Import.Enabled = false);
        Assert.Equal(SubsystemStates.Idle, (await ReadAsync(disabled)).Status);
    }
}
