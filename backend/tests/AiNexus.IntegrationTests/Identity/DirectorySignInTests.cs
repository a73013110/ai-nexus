using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Identity;

public sealed class DirectorySignInTests
{
    private const string Sid = "S-1-5-21-1000-2000-3000-4242";

    // 每個 scope 各自一條連線，等於同一人在兩個程序同時第一次登入：要排隊並在鎖內重新比對，不能撞上唯一索引變成 500。
    [Fact]
    public async Task ConcurrentFirstSignInsOfOneAccountCreateOneUser()
    {
        await using var factory = new AppFactory();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<DirectorySignIn>().ResolveAsync(Sid, "erin", new DirectoryProfile("Erin Chen", "erin@fixture.test"), CancellationToken.None);
        }));

        Assert.All(results, x => Assert.True(x.IsSuccess));
        Assert.Single(results.Select(x => x.Value!.Id).Distinct());
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(x => x.Sid == Sid, TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.UserRoles.CountAsync(x => x.UserId == results[0].Value!.Id, TestContext.Current.CancellationToken));
    }
}
