using System.Collections.Concurrent;
using System.Data;
using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Data.App.Audit;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Office.Attendance;
using AiNexus.Platform.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class AdministrativeTransactionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // handler 漏了提交（或忽略提交失敗）時，資料沒存，回應也不能是成功。
    [Fact]
    public async Task ReturningSuccessWithoutCommittingFailsTheRequest()
    {
        await using var session = await HrSession.CreateAsync();
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var (db, write, trail) = Services(scope);
        trail.Begin(AttendanceAudit.CardUpdated, session.Bob, new DefaultHttpContext(), AuditRoute.Of(null));

        await using (var transaction = (await write.BeginAsync(Target(db, session.Card.Id), Ct)).Value!)
            (await db.Cards.SingleAsync(x => x.Id == session.Card.Id, Ct)).Note = "沒有提交";

        await Assert.ThrowsAsync<InvalidOperationException>(() => trail.CompleteAsync(AuditEvent.Completed));
        Assert.Empty(db.ChangeTracker.Entries<Card>());
        Assert.Null(await db.Cards.AsNoTracking().Where(x => x.Id == session.Card.Id).Select(x => x.Note).SingleAsync(Ct));
    }

    [Fact]
    public async Task LeavingWithAnErrorRollsBackAndRecordsTheFailureWithTheBeforeValues()
    {
        await using var session = await HrSession.CreateAsync();
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var (db, write, trail) = Services(scope);
        trail.Begin(AttendanceAudit.CardUpdated, session.Bob, new DefaultHttpContext(), AuditRoute.Of(null));

        await using (var transaction = (await write.BeginAsync(Target(db, session.Card.Id), Ct)).Value!)
            (await db.Cards.SingleAsync(x => x.Id == session.Card.Id, Ct)).Note = "回滾";
        await trail.CompleteAsync("refused");

        var audit = await db.AuditEvents.AsNoTracking().OrderBy(x => x.Id).LastAsync(x => x.Action == AttendanceAudit.CardUpdated.Name, Ct);
        Assert.Equal("refused", audit.Result);
        Assert.Contains("before", audit.DetailsJson!);
        Assert.DoesNotContain("after", audit.DetailsJson!);
        Assert.Null(await db.Cards.AsNoTracking().Where(x => x.Id == session.Card.Id).Select(x => x.Note).SingleAsync(Ct));
    }

    [Fact]
    public async Task CommittingSavesTheChangeWithItsAuditInOneTransaction()
    {
        await using var session = await HrSession.CreateAsync();
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var (db, write, trail) = Services(scope);
        trail.Begin(AttendanceAudit.CardUpdated, session.Bob, new DefaultHttpContext(), AuditRoute.Of(null));

        await using (var transaction = (await write.BeginAsync(Target(db, session.Card.Id), Ct)).Value!)
        {
            (await db.Cards.SingleAsync(x => x.Id == session.Card.Id, Ct)).HiddenFromRecords = true;
            Assert.True((await transaction.CommitAsync(Ct)).IsSuccess);
        }
        await trail.CompleteAsync(AuditEvent.Completed);

        var audit = await db.AuditEvents.AsNoTracking().OrderBy(x => x.Id).LastAsync(x => x.Action == AttendanceAudit.CardUpdated.Name, Ct);
        Assert.Equal(AuditEvent.Completed, audit.Result);
        Assert.Contains("before", audit.DetailsJson!);
        Assert.Contains("after", audit.DetailsJson!);
        Assert.True(await db.Cards.AsNoTracking().Where(x => x.Id == session.Card.Id).Select(x => x.HiddenFromRecords).SingleAsync(Ct));
    }

    [Fact]
    public async Task ALockThatCannotBeAcquiredIsBusyAndReleasesWhatItAlreadyHeld()
    {
        await using var session = await HrSession.CreateAsync();
        var (first, second) = ("test:first", "test:second");
        await using var holder = session.Factory.Services.CreateAsyncScope();
        await using (await Services(holder).Db.Database.BeginTransactionAsync(Ct))
        {
            Assert.True(await holder.ServiceProvider.GetRequiredService<AdministrativeWriteLock>().TryAcquireAsync(second, Ct));
            await using var scope = session.Factory.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AccessControlOptions>>().Value.WriteLockTimeoutSeconds = 1;
            var (db, write, _) = Services(scope);

            var busy = await write.BeginAsync(Target(db, session.Card.Id).Locking(second, first), Ct);

            Assert.Equal(AccessControlErrors.WriteBusy, busy.Error);
            // first 排序在前、已經取得：失敗時要跟著交易放掉，別人才拿得到。
            await using var other = session.Factory.Services.CreateAsyncScope();
            await using (await Services(other).Db.Database.BeginTransactionAsync(Ct))
                Assert.True(await other.ServiceProvider.GetRequiredService<AdministrativeWriteLock>().TryAcquireAsync(first, Ct));
        }
        // 兩把鎖都放開後，同樣的寫入就能開始。
        await using var again = session.Factory.Services.CreateAsyncScope();
        var (againDb, againWrite, _) = Services(again);
        var begun = await againWrite.BeginAsync(Target(againDb, session.Card.Id).Locking(second, first), Ct);
        Assert.True(begun.IsSuccess);
        await begun.Value.DisposeAsync();
    }

    // handler 在提交之後還有工作（重讀 DTO 等）時，using 雖然還沒結束，也不能再佔連線與鎖。
    [Fact]
    public async Task CommittingReleasesTheConnectionAndLocksBeforeTheHandlerReturns()
    {
        await using var session = await HrSession.CreateAsync();
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var (db, write, _) = Services(scope);

        await using var transaction = (await write.BeginAsync(Target(db, session.Card.Id).Locking("test:held"), Ct)).Value!;
        (await db.Cards.SingleAsync(x => x.Id == session.Card.Id, Ct)).HiddenFromRecords = true;
        Assert.True((await transaction.CommitAsync(Ct)).IsSuccess);

        AssertReleased(db);
        await AssertLockFreeAsync(session, "test:held");
    }

    // 撞上別人時也一樣：回應還沒送出，鎖就要放開，等的人才不必多等。
    [Fact]
    public async Task AConflictAtCommitRollsBackImmediately()
    {
        await using var session = await HrSession.CreateAsync();
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var (db, write, _) = Services(scope);

        await using var transaction = (await write.BeginAsync(Target(db, session.Card.Id).Locking("test:held"), Ct)).Value!;
        var card = await db.Cards.SingleAsync(x => x.Id == session.Card.Id, Ct);
        db.ExpectRowVersion(card, card.RowVersion - 1);
        card.HiddenFromRecords = true;

        Assert.Equal(AccessControlErrors.WriteConflict, (await transaction.CommitAsync(Ct)).Error);

        AssertReleased(db);
        Assert.Empty(db.ChangeTracker.Entries<Card>());
        await AssertLockFreeAsync(session, "test:held");
    }

    // 第二次提交時交易已結束，存下去的變更會落在交易之外。
    [Fact]
    public async Task CommittingTwiceIsRefused()
    {
        await using var session = await HrSession.CreateAsync();
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var (db, write, _) = Services(scope);

        await using var transaction = (await write.BeginAsync(Target(db, session.Card.Id), Ct)).Value!;
        var card = await db.Cards.SingleAsync(x => x.Id == session.Card.Id, Ct);
        card.HiddenFromRecords = true;
        Assert.True((await transaction.CommitAsync(Ct)).IsSuccess);

        card.Note = "交易之外";
        await Assert.ThrowsAsync<ObjectDisposedException>(() => transaction.CommitAsync(Ct));
        Assert.Null(await db.Cards.AsNoTracking().Where(x => x.Id == session.Card.Id).Select(x => x.Note).SingleAsync(Ct));
    }

    [Fact]
    public async Task HoldingTheTransactionAsLongAsOthersWaitForTheLockIsLogged()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var logs = new Logs();
        await using var factory = new AppFactory(clock: clock, services: services => services.AddSingleton<ILoggerProvider>(logs));
        await using var session = await HrSession.CreateAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, write, _) = Services(scope);
        var limit = TimeSpan.FromSeconds(scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AccessControlOptions>>().Value.WriteLockTimeoutSeconds);

        await using (var quick = (await write.BeginAsync(Target(db, session.Card.Id), Ct)).Value!)
        {
            clock.Advance(limit - TimeSpan.FromSeconds(1));
            await quick.CommitAsync(Ct);
        }
        Assert.DoesNotContain(HeldTooLong, logs.Ids);

        await using (var slow = (await write.BeginAsync(Target(db, session.Card.Id), Ct)).Value!)
        {
            clock.Advance(limit);
            await slow.CommitAsync(Ct);
        }
        Assert.Contains(HeldTooLong, logs.Ids);
    }

    private const int HeldTooLong = 1004;

    private static void AssertReleased(AppDbContext db)
    {
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    private static async Task AssertLockFreeAsync(HrSession session, string name)
    {
        await using var other = session.Factory.Services.CreateAsyncScope();
        other.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AccessControlOptions>>().Value.WriteLockTimeoutSeconds = 1;
        await using (await Services(other).Db.Database.BeginTransactionAsync(Ct))
            Assert.True(await other.ServiceProvider.GetRequiredService<AdministrativeWriteLock>().TryAcquireAsync(name, Ct));
    }

    private sealed class Logs : ILoggerProvider
    {
        public ConcurrentQueue<int> Ids { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(Ids);
        public void Dispose() { }

        private sealed class Capture(ConcurrentQueue<int> ids) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) => ids.Enqueue(id.Id);
        }
    }

    private static (AppDbContext Db, AdministrativeWrite Write, AuditTrail Trail) Services(AsyncServiceScope scope)
        => (scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<AdministrativeWrite>(), scope.ServiceProvider.GetRequiredService<AuditTrail>());

    private static AuditTarget Target(AppDbContext db, Guid id) => AuditTarget.Of(id, async ct =>
        await db.Cards.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.CardNumber, x.HiddenFromRecords }).SingleOrDefaultAsync(ct));
}
