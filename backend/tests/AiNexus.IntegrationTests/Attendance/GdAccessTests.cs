using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Features.Staff.Attendance.Gd;
using Microsoft.Extensions.Logging;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class GdAccessTests
{
    private const string UsersSql = "SELECT [ID] FROM [dbo].[Sys_User]";

    [Fact]
    public async Task AnUnreachableGdIsLoggedOnceAndTheRestorationOnceMore()
    {
        using var gd = FakeGd.Create();
        gd.AddUser("G001", "alice");
        var options = FakeGd.Unreachable();
        var log = new Events();
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
        var log = new Events();

        var rows = await FakeGd.AccessTo(FakeGd.NotConfigured(), log).QueryAsync<string>(UsersSql, null, TestContext.Current.CancellationToken);

        Assert.Null(rows);
        Assert.Equal([3407], log.Recorded.Select(x => x.Id));
    }

    [Fact]
    public async Task AQueryGdRejectsIsUnavailableToo()
    {
        using var gd = FakeGd.Create();

        // GD 端改了結構（表不見了）與連不上一樣，出勤頁都只能少掉假單。
        Assert.Null(await gd.Access().QueryAsync<string>("SELECT [ID] FROM [dbo].[Missing]", null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACancelledQueryIsNotTreatedAsUnavailable()
    {
        using var gd = FakeGd.Create();
        var log = new Events();
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

    private sealed class Events : ILogger<GdAccess>
    {
        public List<(int Id, LogLevel Level)> Recorded { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Recorded.Add((eventId.Id, logLevel));
    }
}
