using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Office.Attendance.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.PunchSource;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ImportPunchFilesTests
{
    private static readonly string Oct1 = NameOf(10, 1);
    private static readonly string Oct2 = NameOf(10, 2);

    [Fact]
    public async Task AScanStoresEachLineInTaipeiTimeWithTheMachineAndEventCodesUntouched()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 29, 15), "", Line("A001", 10, 1, 18, 2, 3, machine: "002", eventCode: "15"), Line("B002", 10, 1, 8, 45)]);

        var scan = await session.ScanAsync();

        Assert.Equal((true, 1, 1, 0), (scan.SourceAvailable, scan.Found, scan.Imported, scan.Failed));
        using var scope = session.Factory.Services.CreateScope();
        var punches = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Punch>().AsNoTracking().OrderBy(x => x.PunchedAt).ThenBy(x => x.CardNumber).ToListAsync();
        Assert.Equal([new DateTimeOffset(2026, 10, 1, 8, 29, 15, Taipei), At(10, 1, 8, 45), new DateTimeOffset(2026, 10, 1, 18, 2, 3, Taipei)], punches.Select(x => x.PunchedAt));
        Assert.All(punches, x => Assert.Equal(Taipei, x.PunchedAt.Offset));
        Assert.Equal(("002", "15"), (punches[2].MachineCode, punches[2].EventCode));
        Assert.Equal(("001", "11"), (punches[0].MachineCode, punches[0].EventCode));
        var import = Assert.Single(await session.ImportsAsync());
        Assert.Equal((PunchImportStatuses.Succeeded, PunchImportTriggers.Scheduled, 3), (import.Status, import.TriggerKind, import.PunchCount));
        Assert.Null(import.RequestedById);
        Assert.Null(import.ErrorMessage);
        var file = await session.FileAsync(Oct1);
        Assert.Equal((import.SourceModifiedAt, import.SourceSize, import.Id), (file.ImportedModifiedAt, file.ImportedSize, file.LastImportId));
    }

    [Fact]
    public async Task ALateNightPunchInTheNextDaysFileBelongsToTheDayItWasMade()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct2, [Line("A001", 10, 1, 23, 58), Line("A001", 10, 2, 0, 3), Line("A001", 10, 2, 8, 20)]);

        await session.ScanAsync();

        using var scope = session.Factory.Services.CreateScope();
        var dates = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Punch>().AsNoTracking().OrderBy(x => x.PunchedAt).Select(x => x.AttendanceDate).ToListAsync();
        Assert.Equal([new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 2)], dates);
    }

    [Theory]
    [InlineData("001:A1:20261001:080000", "欄位數")]
    [InlineData("001:A1:20261332:080000:11", "日期")]
    [InlineData("001:A1:20261001:256100:11", "時間")]
    [InlineData("001::20261001:080000:11", "卡號")]
    public async Task AMalformedLineFailsTheWholeFileAndLeavesNoPartialPunches(string bad, string reason)
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), bad, Line("A002", 10, 1, 8, 5)]);

        var scan = await session.ScanAsync();

        Assert.Equal((0, 1), (scan.Imported, scan.Failed));
        Assert.Empty(await session.PunchesAsync());
        var import = Assert.Single(await session.ImportsAsync());
        Assert.Equal((PunchImportStatuses.Failed, 0), (import.Status, import.PunchCount));
        Assert.Contains("第 2 行", import.ErrorMessage);
        Assert.Contains(reason, import.ErrorMessage);
        // 沒成功過的檔案沒有已匯入的版本，但仍有檔案列，指向這次失敗。
        var file = await session.FileAsync(Oct1);
        Assert.Null(file.ImportedModifiedAt);
        Assert.Null(file.ImportedSize);
        Assert.Equal(import.Id, file.LastImportId);
    }

    [Fact]
    public async Task AFileOverTheSizeLimitFailsAndTellsTheLimit()
    {
        await using var session = await PunchImportSession.CreateAsync(options => options.Import.MaxFileBytes = 1024);
        session.Source.Write(Oct1, [.. Enumerable.Range(0, 60).Select(i => Line($"A{i:000}", 10, 1, 8, i))]);

        var scan = await session.ScanAsync();

        Assert.Equal((0, 1), (scan.Imported, scan.Failed));
        Assert.Empty(await session.PunchesAsync());
        // 上限不到 1 MB 時以 KB 表示，不會顯示成「0 MB」。
        Assert.Contains("1 KB", Assert.Single(await session.ImportsAsync()).ErrorMessage);
    }

    [Fact]
    public async Task ReimportingAChangedFileReplacesWhatItImportedBefore()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 12, 0), Line("B002", 10, 1, 8, 30)]);
        await session.ScanAsync();
        Assert.Equal(3, (await session.PunchesAsync()).Count);

        // 打卡機中午多輸出一次、晚上再覆蓋：每張卡的最晚刷卡換成新的，A001 的 12:00 不見了，C003 是新來的。
        session.Clock.Advance(TimeSpan.FromHours(6));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 5), Line("B002", 10, 1, 8, 30), Line("C003", 10, 1, 9, 0)]);
        var scan = await session.ScanAsync();

        Assert.Equal(1, scan.Imported);
        Assert.Equal(
            [("A001", At(10, 1, 8, 0)), ("B002", At(10, 1, 8, 30)), ("C003", At(10, 1, 9, 0)), ("A001", At(10, 1, 18, 5))],
            (await session.PunchesAsync()).Select(x => (x.Card, x.At)));
        var imports = await session.ImportsAsync(Oct1);
        Assert.Equal([3, 4], imports.Select(x => x.PunchCount));
        Assert.Equal(imports[1].Id, (await session.FileAsync(Oct1)).LastImportId);
    }

    [Fact]
    public async Task AFileThatLostAllItsLinesEmptiesItsPunches()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await session.ScanAsync();

        session.Clock.Advance(TimeSpan.FromHours(1));
        session.Source.Write(Oct1, [], age: TimeSpan.FromMinutes(30));
        await session.ScanAsync();

        Assert.Empty(await session.PunchesAsync());
        Assert.Equal([1, 0], (await session.ImportsAsync()).Select(x => x.PunchCount));
    }

    [Fact]
    public async Task APunchAlreadyWrittenByAnotherFileIsNotWrittenAgain()
    {
        await using var session = await PunchImportSession.CreateAsync();
        var late = Line("A001", 10, 1, 23, 55);
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), late]);
        session.Source.Write(Oct2, [late, Line("A001", 10, 2, 8, 5)]);

        var scan = await session.ScanAsync();

        Assert.Equal(2, scan.Imported);
        Assert.Equal(
            [("A001", At(10, 1, 8, 0), Oct1), ("A001", At(10, 1, 23, 55), Oct1), ("A001", At(10, 2, 8, 5), Oct2)],
            (await session.PunchesAsync()).Select(x => (x.Card, x.At, x.File)));
        // 重複的那筆算在先寫入的檔，後一天的檔只算實際寫入的一筆。
        Assert.Equal([2], (await session.ImportsAsync(Oct1)).Select(x => x.PunchCount));
        Assert.Equal([1], (await session.ImportsAsync(Oct2)).Select(x => x.PunchCount));
    }

    [Fact]
    public async Task AFailureInsideTheTransactionRollsEverythingBackAndIsRecordedOutsideIt()
    {
        var failure = new FailAfterImportSaved();
        await using var session = await PunchImportSession.CreateAsync(services: services => services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(failure)));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 12, 0)]);
        await session.ScanAsync();
        var before = await session.PunchesAsync();
        var imported = await session.FileAsync(Oct1);

        // 新內容的刪除與寫入都已送進資料庫之後才失敗：交易必須讓它們全部消失。
        session.Clock.Advance(TimeSpan.FromHours(6));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 0), Line("B002", 10, 1, 9, 0)]);
        failure.Armed = true;
        var scan = await session.ScanAsync();

        Assert.Equal((0, 1), (scan.Imported, scan.Failed));
        Assert.Equal(before, await session.PunchesAsync());
        var imports = await session.ImportsAsync(Oct1);
        Assert.Equal([PunchImportStatuses.Succeeded, PunchImportStatuses.Failed], imports.Select(x => x.Status));
        Assert.Equal(0, imports[1].PunchCount);
        Assert.Contains(nameof(InvalidOperationException), imports[1].ErrorMessage);
        // 檔案列仍是上次成功的版本，只有 LastImportId 前進到失敗那一次；下次內容有變就會重試。
        var file = await session.FileAsync(Oct1);
        Assert.Equal((imported.ImportedModifiedAt, imported.ImportedSize, imports[1].Id), (file.ImportedModifiedAt, file.ImportedSize, file.LastImportId));
    }

    [Fact]
    public async Task ImportsOfTheSameFileNeverRunAtTheSameTime()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 0)]);
        var gate = session.Factory.Services.GetRequiredService<PunchImportGate>();

        await gate.Gate.WaitAsync(TestContext.Current.CancellationToken);
        var manual = session.ReimportAsync(Oct1);
        var scheduled = session.ScanAsync();
        // 鎖被別的匯入佔住時，手動與排程都只能等；這是不讓同一檔被兩個匯入同時處理的保證。
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.False(manual.IsCompleted);
        Assert.False(scheduled.IsCompleted);
        gate.Gate.Release();

        Assert.True((await manual).IsSuccess);
        await scheduled;
        Assert.Equal(2, (await session.PunchesAsync()).Count);
        Assert.DoesNotContain(await session.ImportsAsync(), x => x.Status == PunchImportStatuses.Failed);
    }

    [Fact]
    public async Task ConcurrentManualAndScheduledImportsOfOneFileAllSucceedWithoutDuplicates()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 0)]);

        var scheduled = session.ScanAsync();
        var first = session.ReimportAsync(Oct1);
        var second = session.ReimportAsync(Oct1);
        await Task.WhenAll(scheduled, first, second);

        Assert.True((await first).IsSuccess);
        Assert.True((await second).IsSuccess);
        Assert.Equal(2, (await session.PunchesAsync()).Count);
        var imports = await session.ImportsAsync();
        Assert.DoesNotContain(imports, x => x.Status == PunchImportStatuses.Failed);
        Assert.Equal(2, imports.Count(x => x.TriggerKind == PunchImportTriggers.Manual));
    }

    /// <summary>
    /// 匯入列（成功）存進資料庫之後才拋例外，此時刷卡的刪除與新增已經送出；用來證明回復涵蓋整個交易。
    /// 失敗紀錄是另一次儲存（狀態為 failed），不受影響。
    /// </summary>
    private sealed class FailAfterImportSaved : SaveChangesInterceptor
    {
        private volatile bool pending;

        public volatile bool Armed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            pending = Armed && eventData.Context!.ChangeTracker.Entries<PunchImport>().Any(x => x.State == EntityState.Added && x.Entity.Status == PunchImportStatuses.Succeeded);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (!pending) return base.SavedChangesAsync(eventData, result, cancellationToken);
            pending = false;
            throw new InvalidOperationException("Injected failure after the punches were saved.");
        }
    }
}
