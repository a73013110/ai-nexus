using AiNexus.Data.App.Attendance;
using AiNexus.Features.Staff.Attendance.Imports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static AiNexus.IntegrationTests.Support.PunchSource;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class PunchImportScanTests
{
    private static readonly string Oct1 = NameOf(10, 1);

    [Fact]
    public async Task AFileThatDidNotChangeIsNotImportedAgain()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        Assert.Equal(1, (await session.ScanAsync()).Imported);

        session.Clock.Advance(TimeSpan.FromHours(1));
        var second = await session.ScanAsync();
        session.Clock.Advance(TimeSpan.FromHours(1));
        var third = await session.ScanAsync();

        Assert.Equal((1, 0, 0), (second.Found, second.Imported, second.Failed));
        Assert.Equal(0, third.Imported);
        Assert.Single(await session.ImportsAsync());
    }

    [Fact]
    public async Task ADifferentModifiedTimeOrSizeImportsTheFileAgain()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await session.ScanAsync();
        var original = (await session.FileAsync(Oct1)).ImportedModifiedAt!.Value;

        // 內容不變、只有修改時間不同。
        session.Clock.Advance(TimeSpan.FromHours(1));
        session.Source.SetModified(Oct1, original.AddMinutes(10));
        Assert.Equal(1, (await session.ScanAsync()).Imported);
        Assert.Equal(original.AddMinutes(10), (await session.FileAsync(Oct1)).ImportedModifiedAt);

        // 修改時間不變、只有大小不同（內容多了一行）。
        session.Clock.Advance(TimeSpan.FromHours(1));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 0)]);
        session.Source.SetModified(Oct1, original.AddMinutes(10));
        var scan = await session.ScanAsync();

        Assert.Equal(1, scan.Imported);
        Assert.Equal([1, 1, 2], (await session.ImportsAsync()).Select(x => x.PunchCount));
        Assert.Equal(2, (await session.PunchesAsync()).Count);
    }

    [Fact]
    public async Task AFileWhoseLastImportFailedIsRetriedAfterTheBackoffOrAsSoonAsItChanges()
    {
        await using var session = await PunchImportSession.CreateAsync(options => options.Import.FailedRetrySeconds = 600);
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), "broken"]);
        Assert.Equal(1, (await session.ScanAsync()).Failed);

        // 檔案沒變又在等待時間內：不再多記一筆失敗。
        session.Clock.Advance(TimeSpan.FromMinutes(5));
        var early = await session.ScanAsync();
        Assert.Equal((0, 0), (early.Imported, early.Failed));
        Assert.Single(await session.ImportsAsync());

        // 過了等待時間，檔案雖然沒變，失敗的檔還是重試。
        session.Clock.Advance(TimeSpan.FromMinutes(6));
        var retried = await session.ScanAsync();
        Assert.Equal((0, 1), (retried.Imported, retried.Failed));
        Assert.Equal(2, (await session.ImportsAsync()).Count);

        // 檔案一修好（內容有變）就立刻重匯，不必等。
        session.Clock.Advance(TimeSpan.FromSeconds(1));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        var fixedUp = await session.ScanAsync();
        Assert.Equal((1, 0), (fixedUp.Imported, fixedUp.Failed));
        var imports = await session.ImportsAsync();
        Assert.Equal([PunchImportStatuses.Failed, PunchImportStatuses.Failed, PunchImportStatuses.Succeeded], imports.Select(x => x.Status));
        var file = await session.FileAsync(Oct1);
        Assert.Equal((imports[2].SourceModifiedAt, imports[2].Id), (file.ImportedModifiedAt, file.LastImportId));
        Assert.Single(await session.PunchesAsync());

        // 成功之後恢復正常，不再掃出工作。
        session.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal((0, 0), ((await session.ScanAsync()).Imported, 0));
    }

    [Fact]
    public async Task ARetryHappensOnEveryScanWhenTheBackoffIsZero()
    {
        await using var session = await PunchImportSession.CreateAsync(options => options.Import.FailedRetrySeconds = 0);
        session.Source.Write(Oct1, ["broken"]);

        await session.ScanAsync();
        session.Clock.Advance(TimeSpan.FromSeconds(1));
        await session.ScanAsync();

        Assert.Equal(2, (await session.ImportsAsync()).Count);
    }

    [Fact]
    public async Task AFileStillBeingWrittenWaitsUntilItHasSettled()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)], age: TimeSpan.FromSeconds(5));

        var early = await session.ScanAsync();
        Assert.Equal((1, 0, 1), (early.Found, early.Imported, early.Waiting));
        Assert.Empty(await session.ImportsAsync());

        session.Clock.Advance(TimeSpan.FromSeconds(30));
        var settled = await session.ScanAsync();

        Assert.Equal((1, 0), (settled.Imported, settled.Waiting));
    }

    [Fact]
    public async Task OnlyDailyFilesAreImportedOldestFirst()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(NameOf(10, 3), [Line("A001", 10, 3, 8, 0)]);
        session.Source.Write(NameOf(10, 1), [Line("A001", 10, 1, 8, 0)]);
        session.Source.Write("notes.txt", ["not a punch file"]);
        session.Source.Write("20261301.txt", [Line("A001", 10, 1, 9, 0)]);
        session.Source.Write("20261001.txt.bak", [Line("A001", 10, 1, 9, 0)]);
        session.Source.Write("20261002.csv", [Line("A001", 10, 2, 9, 0)]);

        var scan = await session.ScanAsync();

        Assert.Equal((2, 2, 0), (scan.Found, scan.Imported, scan.Failed));
        Assert.Equal(["20261001.txt", "20261003.txt"], (await session.PunchesAsync()).Select(x => x.File));
    }

    [Fact]
    public async Task AnUnreadableSourceFolderIsReportedOnceAndRecovers()
    {
        await using var session = await PunchImportSession.CreateAsync();
        var state = session.Factory.Services.GetRequiredService<PunchImportState>();
        var log = new LogRecorder<PunchImportWorker>();
        Directory.Delete(session.Source.Folder, recursive: true);

        var down = await session.ScanAsync(log);

        Assert.Equal(new PunchScanResult(false, 0, 0, 0, 0), down);
        Assert.NotNull(state.SourceError);
        // 沒有檔案可記錄，所以不會有匯入列。
        Assert.Empty(await session.ImportsAsync());
        // 中斷期間每個週期都讀不到，日誌只在轉換時記一次。
        Assert.False((await session.ScanAsync(log)).SourceAvailable);
        Assert.Equal([(3403, LogLevel.Warning)], log.Recorded);

        Directory.CreateDirectory(session.Source.Folder);
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        var up = await session.ScanAsync(log);

        Assert.Equal((true, 1), (up.SourceAvailable, up.Imported));
        Assert.Null(state.SourceError);
        Assert.Equal([3403, 3404], log.Recorded.Select(x => x.Id));
    }
}
