using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Identity;

public sealed class LocalAuthenticatorTests
{
    // 每個 scope 各自一條連線，等於不同程序同時驗證：失敗次數不能互相蓋掉，否則併發猜密碼就碰不到鎖定門檻。
    [Fact]
    public async Task ConcurrentFailuresOnOneAccountAreAllCounted()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var id = await CreateUser(admin, LocalAccount());

        var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AuthenticateAsync(factory, "tester", "wrong password")));

        Assert.All(attempts, x => Assert.Equal("invalid_credentials", x.Error?.Code));
        Assert.Equal(4, (await UserAsync(factory, id)).FailedLogins);
    }

    [Fact]
    public async Task AValidationWaitsOnlyForItsOwnAccount()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var busy = await CreateUser(admin, LocalAccount("busy"));
        await CreateUser(admin, LocalAccount("other"));
        var ct = TestContext.Current.CancellationToken;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Users.Where(x => x.Id == busy).ExecuteUpdateAsync(p => p.SetProperty(x => x.FailedLogins, x => x.FailedLogins), ct);

        Assert.True((await AuthenticateAsync(factory, "other", Password).WaitAsync(TimeSpan.FromSeconds(30), ct)).IsSuccess);
        var waiting = AuthenticateAsync(factory, "busy", Password);
        await Task.Delay(200, ct);
        Assert.False(waiting.IsCompleted);

        await transaction.CommitAsync(ct);
        Assert.True((await waiting).IsSuccess);
    }

    private static async Task<Result<User>> AuthenticateAsync(AppFactory factory, string account, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LocalAuthenticator>().AuthenticateAsync(account, password, CancellationToken.None);
    }

    private static async Task<User> UserAsync(AppFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.Id == id);
    }
}
