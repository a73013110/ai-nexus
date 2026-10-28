using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Data.Tables;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.PunchSource;

namespace AiNexus.IntegrationTests.Platform;

/// <summary>超過門檻時由資料庫篩選、排序與分頁；確認運算式能翻成 SQL，且換頁不重複、不漏列。</summary>
public sealed class TableQueryTests
{
    private static readonly string Oct1 = NameOf(10, 1);

    private static TableColumns<PunchImport> Columns(AppDbContext db) => new TableColumns<PunchImport>
    {
        CompleteUpTo = 5,
    }
        .Range("startedAt", x => x.StartedAt)
        .Text("fileName", x => db.Set<PunchFile>().Where(f => f.Id == x.PunchFileId).Select(f => f.FileName).FirstOrDefault())
        .Options("triggerKind", x => x.TriggerKind)
        .Range("punchCount", x => x.PunchCount)
        .Key(x => x.Id)
        .DefaultSort("startedAt", descending: true);

    [Fact]
    public async Task PagesFiltersAndSortsInTheDatabasePastTheThreshold()
    {
        await using var session = await PunchImportSession.CreateAsync();
        session.Source.Write(Oct1, [Line("A001", 10, 1, 8, 0)]);
        await session.ScanAsync();
        for (var i = 0; i < 8; i++)
        {
            session.Clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True((await session.ReimportAsync(Oct1)).IsSuccess);
        }
        using var scope = session.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var imports = db.Set<PunchImport>().AsNoTracking();
        var ct = TestContext.Current.CancellationToken;

        var first = (await imports.ToTablePageAsync(new TableRequest { Limit = 4 }, Columns(db), q => q.Select(x => x.Id), ct)).Value!;
        var second = (await imports.ToTablePageAsync(new TableRequest { Limit = 4, Offset = 4 }, Columns(db), q => q.Select(x => x.Id), ct)).Value!;
        var filtered = (await imports.ToTablePageAsync(new TableRequest { Filter = ["triggerKind:in:manual", "fileName:has:1001"], Sort = "startedAt" }, Columns(db), q => q.Select(x => x.StartedAt), ct)).Value!;

        Assert.False(first.Complete);
        Assert.Equal((9, 4, 4), (first.Total, first.Items.Count, second.Items.Count));
        Assert.Empty(first.Items.Intersect(second.Items));
        Assert.Equal(8, filtered.Total);
        Assert.Equal(filtered.Items.Order(), filtered.Items);
    }
}
