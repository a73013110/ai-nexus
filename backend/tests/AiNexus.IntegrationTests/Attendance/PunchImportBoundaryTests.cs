using AiNexus.Data.App.Attendance;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.PunchSource;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>
/// 同一筆刷卡出現在兩個檔：它屬於先匯入的檔，另一份被當成重複略過。手動重匯從擁有它的檔刪掉時，另一份不可以跟著消失
/// （ADR 0018、<c>PunchImporter</c> 的鄰檔補救）。重複的來源是 HAMS 畫面的手動轉出：它把刷卡寫進刷卡日期的檔，排程轉出則寫進執行當天的檔。
/// </summary>
public sealed class PunchImportBoundaryTests
{
    private static readonly string Oct1 = NameOf(10, 1);
    private static readonly string Oct2 = NameOf(10, 2);
    private static readonly string LatePunch = Line("A001", 10, 1, 23, 55);

    /// <summary>10/1 深夜的刷卡由隔天執行的排程寫進 10/2 的檔，又被手動轉出寫進 10/1 的檔；依檔名日期由舊到新匯入，歸 10/1。</summary>
    private static async Task<PunchImportSession> SessionWithADuplicateAsync()
    {
        var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), LatePunch]);
        session.Source.Write(Oct2, [LatePunch, Line("A001", 10, 2, 8, 5)]);
        await session.ScanAsync();
        Assert.Equal(Oct1, Assert.Single(await session.PunchesAsync(), x => x.At == At(10, 1, 23, 55)).File);
        return session;
    }

    [Fact]
    public async Task AManualReimportThatDropsAPunchQueuesTheLaterFileSoItsCopyIsKept()
    {
        await using var session = await SessionWithADuplicateAsync();
        session.Clock.Advance(TimeSpan.FromHours(6));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);

        var reimport = await session.ReimportAsync(Oct1);

        Assert.True(reimport.IsSuccess);
        // 擁有者重匯後這筆確實從 10/1 的檔消失，但之後的檔已被標記為要重匯（版本欄位清空）。
        Assert.DoesNotContain(await session.PunchesAsync(), x => x.At == At(10, 1, 23, 55));
        var next = await session.FileAsync(Oct2);
        Assert.Null(next.ImportedModifiedAt);
        Assert.Null(next.ImportedSize);

        var scan = await session.ScanAsync();

        Assert.Equal(1, scan.Imported);
        Assert.Equal(
            [("A001", At(10, 1, 8, 0), Oct1), ("A001", At(10, 1, 23, 55), Oct2), ("A001", At(10, 2, 8, 5), Oct2)],
            (await session.PunchesAsync()).Select(x => (x.Card, x.At, x.File)));
        Assert.NotNull((await session.FileAsync(Oct2)).ImportedModifiedAt);
        // 補救不會一直循環：10/2 內容沒變，再掃一次什麼都不做。
        session.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(0, (await session.ScanAsync()).Imported);
    }

    [Fact]
    public async Task EveryLaterFileIsQueuedButEarlierOnesAreNot()
    {
        await using var session = await PunchImportSession.CreateAsync();
        // HAMS 主機停了幾天，恢復後的排程把 10/1 的刷卡寫進 10/5 的檔；10/1 的檔是手動轉出的。
        session.Source.Write(NameOf(9, 30), [Line("A001", 9, 30, 8, 0)]);
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 0)]);
        session.Source.Write(NameOf(10, 5), [Line("A001", 10, 1, 18, 0), Line("A001", 10, 5, 8, 0)]);
        await session.ScanAsync();

        session.Clock.Advance(TimeSpan.FromHours(6));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        Assert.True((await session.ReimportAsync(Oct1)).IsSuccess);

        Assert.NotNull((await session.FileAsync(NameOf(9, 30))).ImportedModifiedAt);
        Assert.Null((await session.FileAsync(NameOf(10, 5))).ImportedModifiedAt);
        Assert.Equal(1, (await session.ScanAsync()).Imported);
        Assert.Equal(NameOf(10, 5), Assert.Single(await session.PunchesAsync(), x => x.At == At(10, 1, 18, 0)).File);
    }

    [Fact]
    public async Task APunchNoOtherFileHasIsDeletedAndTheQueuedFileAddsNothing()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 12, 0)]);
        session.Source.Write(Oct2, [Line("A001", 10, 2, 8, 5)]);
        await session.ScanAsync();

        // 12:00 是誤刷、HR 從檔案拿掉：它只在 10/1 的檔裡，刪除是正確的。程式不讀檔就不知道之後的檔有沒有它，
        // 所以仍會把 10/2 標記並重匯一次（多一次便宜的匯入，內容不變）；之後不會再有任何工作。
        session.Clock.Advance(TimeSpan.FromHours(6));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        Assert.True((await session.ReimportAsync(Oct1)).IsSuccess);
        var scan = await session.ScanAsync();

        Assert.Equal(1, scan.Imported);
        Assert.Equal([At(10, 1, 8, 0), At(10, 2, 8, 5)], (await session.PunchesAsync()).Select(x => x.At));
        Assert.Equal([1, 1], (await session.ImportsAsync(Oct2)).Select(x => x.PunchCount));
        session.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(0, (await session.ScanAsync()).Imported);
        Assert.DoesNotContain(await session.ImportsAsync(), x => x.Status == PunchImportStatuses.Failed);
    }
}
