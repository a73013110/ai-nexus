using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class AdministrativeWriteLockTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task WritesInDifferentModulesDoNotWaitForEachOther()
    {
        await using var session = await Session.CreateAsync();
        await using var held = await session.HoldAsync(FeatureCatalog.Organization);

        // 前一筆還卡在 mutation：如果兩個模組共用一把鎖，這裡會等到逾時。
        var other = await session.WriteAsync(FeatureCatalog.MailMessagesResend).WaitAsync(Limit, TestContext.Current.CancellationToken);

        Assert.True(other.IsSuccess);
        Assert.True((await held.FinishAsync()).IsSuccess);
    }

    [Theory]
    [InlineData(FeatureCatalog.Organization, FeatureCatalog.OrganizationImport)]
    // 帳號與角色都會改變權限，不同模組也要排隊，否則互相撤權會雙雙通過 lockout 檢查。
    [InlineData(FeatureCatalog.IdentityUsers, FeatureCatalog.AccessControlRoles)]
    public async Task WritesInTheSameScopeRunOneAtATime(string first, string second)
    {
        await using var session = await Session.CreateAsync();
        await using var held = await session.HoldAsync(first);
        var entered = new TaskCompletionSource();

        var next = session.WriteAsync(second, entered);
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
        await using var held = await session.HoldAsync(FeatureCatalog.Organization);
        var entered = new TaskCompletionSource();

        var late = await session.WriteAsync(FeatureCatalog.Organization, entered).WaitAsync(Limit, TestContext.Current.CancellationToken);

        Assert.Equal(AccessControlErrors.WriteBusy, late.Error);
        Assert.False(entered.Task.IsCompleted);
        Assert.True((await held.FinishAsync()).IsSuccess);
    }

    [Fact]
    public async Task ARejectedWriteReleasesTheLockWithItsTransaction()
    {
        await using var session = await Session.CreateAsync();
        var rejected = AccessControlErrors.AdminResourceNotFound;

        var outcome = await session.WriteAsync(FeatureCatalog.Organization, mutation: () => Task.FromResult<Result>(rejected));
        var next = await session.WriteAsync(FeatureCatalog.Organization).WaitAsync(Limit, TestContext.Current.CancellationToken);

        Assert.Equal(rejected, outcome.Error);
        Assert.True(next.IsSuccess);
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

        var outcome = await session.WriteAsync(FeatureCatalog.Organization, scope, () =>
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

        var outcome = await session.WriteAsync(FeatureCatalog.IdentityUsers, scope, async () =>
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
        public async Task<Result> WriteAsync(string feature, TaskCompletionSource? entered = null, Func<Task<Result>>? mutation = null)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await WriteAsync(feature, scope, mutation ?? (() => { entered?.TrySetResult(); return Task.FromResult(Result.Success); }));
        }

        public Task<Result> WriteAsync(string feature, AsyncServiceScope scope, Func<Task<Result>> mutation)
            => scope.ServiceProvider.GetRequiredService<AdministrativeWrite>().ExecuteAsync(actor, AuditTarget.Of(feature, "test", _ => Task.FromResult<object?>(null)), mutation, CancellationToken.None);

        /// <summary>開始一筆寫入，停在 mutation 內直到 <see cref="HeldWrite.FinishAsync"/>，回傳時已持有鎖。</summary>
        public async Task<HeldWrite> HoldAsync(string feature)
        {
            var entered = new TaskCompletionSource();
            var release = new TaskCompletionSource();
            var running = WriteAsync(feature, mutation: async () =>
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
