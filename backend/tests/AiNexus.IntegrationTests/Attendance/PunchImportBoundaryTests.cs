using AiNexus.Data.App.Attendance;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.PunchSource;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>
/// 同一筆刷卡出現在相鄰兩天的檔：它屬於先匯入的檔，之後那個檔重匯而新內容沒有它，不可以讓資料消失（ADR 0018、<c>PunchImporter</c> 的鄰檔補救）。
/// </summary>
public sealed class PunchImportBoundaryTests
{
    private static readonly string Oct1 = NameOf(10, 1);
    private static readonly string Oct2 = NameOf(10, 2);
    private static readonly string LatePunch = Line("A001", 10, 1, 23, 55);

    private static async Task<PunchImportSession> SessionWithBothDaysImportedAsync()
    {
        var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), LatePunch]);
        session.Source.Write(Oct2, [LatePunch, Line("A001", 10, 2, 8, 5)]);
        await session.ScanAsync();
        Assert.Equal(Oct1, Assert.Single(await session.PunchesAsync(), x => x.At == At(10, 1, 23, 55)).File);
        return session;
    }

    [Fact]
    public async Task DroppingAPunchFromTheOwnerFileQueuesTheNextDaysFileSoItIsNotLost()
    {
        await using var session = await SessionWithBothDaysImportedAsync();
        session.Clock.Advance(TimeSpan.FromHours(6));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);

        var reimport = await session.ReimportAsync(Oct1);

        Assert.True(reimport.IsSuccess);
        // 擁有者重匯後這筆確實從 A 檔消失，但後一天的檔已被標記為要重匯（版本欄位清空）。
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
    }

    [Fact]
    public async Task OneScanHealsTheNeighbourWithoutWaitingForTheNextInterval()
    {
        await using var session = await SessionWithBothDaysImportedAsync();
        session.Clock.Advance(TimeSpan.FromHours(6));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);

        var scan = await session.ScanAsync();

        // 先匯入 A（刪掉那一筆並標記 B），同一次掃描的下一輪再匯入 B。
        Assert.Equal((2, 0), (scan.Imported, scan.Failed));
        Assert.Equal(
            [("A001", At(10, 1, 8, 0), Oct1), ("A001", At(10, 1, 23, 55), Oct2), ("A001", At(10, 2, 8, 5), Oct2)],
            (await session.PunchesAsync()).Select(x => (x.Card, x.At, x.File)));
        // 補救不會一直循環：B 內容沒變，再掃一次什麼都不做。
        session.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(0, (await session.ScanAsync()).Imported);
    }

    [Fact]
    public async Task APunchNoOtherFileHasIsStillDeletedAndTheQueuedNeighbourAddsNothing()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 12, 0)]);
        session.Source.Write(Oct2, [Line("A001", 10, 2, 8, 5)]);
        await session.ScanAsync();

        // 中午那筆被晚上的輸出取代：它只在 10/1 的檔裡，刪除是正確的。程式無法不讀檔就知道後一天的檔有沒有它，
        // 所以仍會把它標記並重匯一次（多一次便宜的匯入，內容不變）；之後不會再有任何工作。
        session.Clock.Advance(TimeSpan.FromHours(6));
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), Line("A001", 10, 1, 18, 0)]);
        var scan = await session.ScanAsync();

        Assert.Equal(2, scan.Imported);
        Assert.Equal([At(10, 1, 8, 0), At(10, 1, 18, 0), At(10, 2, 8, 5)], (await session.PunchesAsync()).Select(x => x.At));
        Assert.Equal([1, 1], (await session.ImportsAsync(Oct2)).Select(x => x.PunchCount));
        session.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(0, (await session.ScanAsync()).Imported);
    }

    [Fact]
    public async Task ThePreviousDaysFileIsCoveredToo()
    {
        await using var session = await PunchImportSession.CreateAsync();
        // 後一天的檔先匯入而擁有這筆；之後前一天的檔補進來時，重複的那筆被略過、仍屬於後一天的檔。
        session.Source.Write(Oct2, [LatePunch, Line("A001", 10, 2, 8, 5)]);
        await session.ScanAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0), LatePunch]);
        await session.ScanAsync();
        Assert.Equal(Oct2, Assert.Single(await session.PunchesAsync(), x => x.At == At(10, 1, 23, 55)).File);

        // 後一天的檔重匯、新內容沒有這筆（機器改輸出）：前一天的檔還有，要補回來。
        session.Clock.Advance(TimeSpan.FromHours(6));
        session.Source.Write(Oct2, [Line("A001", 10, 2, 8, 5)]);
        var scan = await session.ScanAsync();

        Assert.Equal(2, scan.Imported);
        Assert.Equal(Oct1, Assert.Single(await session.PunchesAsync(), x => x.At == At(10, 1, 23, 55)).File);
        Assert.Equal(3, (await session.PunchesAsync()).Count);
        Assert.DoesNotContain(await session.ImportsAsync(), x => x.Status == PunchImportStatuses.Failed);
    }
}
