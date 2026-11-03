using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class AdministrativeWriteLockTests
{
    private const string First = "test:first", Second = "test:second";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task WritesThatDeclareDifferentLocksDoNotWaitForEachOther()
    {
        await using var session = await Session.CreateAsync();
        await using var held = await session.HoldAsync(First);

        // 前一筆還卡在交易內：如果共用一把鎖，這裡會等到逾時。
        var other = await session.WriteAsync(Second).WaitAsync(Limit, TestContext.Current.CancellationToken);
        var unlocked = await session.WriteAsync(null).WaitAsync(Limit, TestContext.Current.CancellationToken);

        Assert.True(other.IsSuccess);
        Assert.True(unlocked.IsSuccess);
        Assert.True((await held.FinishAsync()).IsSuccess);
    }

    [Fact]
    public async Task WritesThatDeclareTheSameLockRunOneAtATime()
    {
        await using var session = await Session.CreateAsync();
        await using var held = await session.HoldAsync(First);
        var entered = new TaskCompletionSource();

        var next = session.WriteAsync(First, entered);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.False(entered.Task.IsCompleted);

        Assert.True((await held.FinishAsync()).IsSuccess);
        Assert.True((await next.WaitAsync(Limit, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True(entered.Task.IsCompleted);
    }

    [Fact]
    public async Task WaitingPastTheTimeoutFailsWithoutRunningTheMutation()
    {
        await using var session = await Session.CreateAsync(services => services.PostConfigure<AccessControlOptions>(x => x.WriteLockTimeoutSeconds = 1));
        await using var held = await session.HoldAsync(First);
        var entered = new TaskCompletionSource();

        var late = await session.WriteAsync(First, entered).WaitAsync(Limit, TestContext.Current.CancellationToken);

        Assert.Equal(AccessControlErrors.WriteBusy, late.Error);
        Assert.False(entered.Task.IsCompleted);
        Assert.True((await held.FinishAsync()).IsSuccess);
    }

    // 命令逾時比等鎖的上限短時，也要回「等不到鎖」而不是 SQL 例外；其他命令的逾時不受影響。
    [Fact]
    public async Task WaitingForALockOutlastsAShorterCommandTimeout()
    {
        await using var session = await Session.CreateAsync();
        await using var held = await session.HoldAsync(First);
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
        await using var transaction = await database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        database.SetCommandTimeout(1);

        var acquired = await database.TryAcquireApplicationLockAsync(First, TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.False(acquired);
        Assert.Equal(1, database.GetCommandTimeout());
    }

    [Fact]
    public async Task ARejectedWriteReleasesTheLockWithItsTransaction()
    {
        await using var session = await Session.CreateAsync();
        var rejected = AccessControlErrors.AdminResourceNotFound;

        var outcome = await session.WriteAsync(First, mutation: () => Task.FromResult<Result>(rejected));
        var next = await session.WriteAsync(First).WaitAsync(Limit, TestContext.Current.CancellationToken);

        Assert.Equal(rejected, outcome.Error);
        Assert.True(next.IsSuccess);
    }

    // 兩把鎖的宣告順序相反也不能互等成死結：都依名稱排序後才取。
    [Fact]
    public async Task LocksAreTakenInNameOrderWhateverTheDeclaredOrder()
    {
        await using var session = await Session.CreateAsync();
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var write = scope.ServiceProvider.GetRequiredService<AdministrativeWrite>();
        var target = AuditTarget.Of("test", _ => Task.FromResult<object?>(null)).Locking(Second, First);

        var begun = await write.BeginAsync(target, TestContext.Current.CancellationToken);
        await using var transaction = begun.Value!;

        Assert.Equal([Second, First], target.Locks);
        Assert.True((await transaction.CommitAsync(TestContext.Current.CancellationToken)).IsSuccess);
    }

    // 鎖外的寫入（登入、匯入）仍可能撞上唯一索引：這是時機問題，要回可重送的 409，不是 500。
    [Fact]
    public async Task AUniqueIndexViolationIsAConflictAndRollsBack()
    {
        await using var session = await Session.CreateAsync();
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(new Card { CardNumber = "A001" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var outcome = await Session.WriteAsync(First, scope, () =>
        {
            db.Add(new Card { CardNumber = "A002" });
            db.Add(new Card { CardNumber = "A001" });
            return Task.FromResult(Result.Success);
        });

        Assert.Equal(AccessControlErrors.WriteConflict, outcome.Error);
        Assert.Empty(db.ChangeTracker.Entries<Card>());
        Assert.False(await db.Cards.AnyAsync(x => x.CardNumber == "A002", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AConcurrencyTokenChangedByAnotherWriterIsAConflict()
    {
        await using var session = await Session.CreateAsync();
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var outcome = await Session.WriteAsync(First, scope, async () =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == session.Actor);
            // 別的連線（例如本人改密碼）先提交：交易內的讀取不持有鎖，不會擋住它。
            await using var other = session.Factory.Services.CreateAsyncScope();
            await other.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(x => x.Id == session.Actor)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.SecurityVersion, x => x.SecurityVersion + 1));
            user.DisplayName = "changed";
            return Result.Success;
        });

        Assert.Equal(AccessControlErrors.WriteConflict, outcome.Error);
    }

    private sealed class Session(AppFactory factory, Guid actor) : IAsyncDisposable
    {
        public AppFactory Factory => factory;
        public Guid Actor => actor;

        public static async Task<Session> CreateAsync(Action<IServiceCollection>? services = null)
        {
            var factory = new AppFactory(administrators: ["alice"], services: services);
            using var admin = await factory.SignedInAsync();
            return new(factory, await OrganizationApi.UserIdAsync(factory, "alice"));
        }

        /// <summary>每筆寫入一個 scope（各自的連線），才是真的併行。</summary>
        public async Task<Result> WriteAsync(string? name, TaskCompletionSource? entered = null, Func<Task<Result>>? mutation = null)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await WriteAsync(name, scope, mutation ?? (() => { entered?.TrySetResult(); return Task.FromResult(Result.Success); }));
        }

        public static async Task<Result> WriteAsync(string? name, AsyncServiceScope scope, Func<Task<Result>> mutation)
        {
            var target = AuditTarget.Of("test", _ => Task.FromResult<object?>(null));
            var begun = await scope.ServiceProvider.GetRequiredService<AdministrativeWrite>().BeginAsync(name is null ? target : target.Locking(name), CancellationToken.None);
            if (!begun.IsSuccess) return begun.Error;
            await using var transaction = begun.Value!;
            var outcome = await mutation();
            return outcome.IsSuccess ? await transaction.CommitAsync(CancellationToken.None) : outcome.Error;
        }

        /// <summary>開始一筆寫入，停在交易內直到 <see cref="HeldWrite.FinishAsync"/>，回傳時已持有鎖。</summary>
        public async Task<HeldWrite> HoldAsync(string name)
        {
            var entered = new TaskCompletionSource();
            var release = new TaskCompletionSource();
            var running = WriteAsync(name, mutation: async () =>
            {
                entered.TrySetResult();
                await release.Task;
                return Result.Success;
            });
            await entered.Task.WaitAsync(Limit, TestContext.Current.CancellationToken);
            return new(running, release);
        }

        public ValueTask DisposeAsync() => factory.DisposeAsync();
    }

    private sealed class HeldWrite(Task<Result> running, TaskCompletionSource release) : IAsyncDisposable
    {
        public Task<Result> FinishAsync()
        {
            release.TrySetResult();
            return running.WaitAsync(Limit, TestContext.Current.CancellationToken);
        }

        // 斷言失敗時也要放行，否則釋放 host 會卡在這筆寫入。
        public async ValueTask DisposeAsync() => await FinishAsync();
    }
}
