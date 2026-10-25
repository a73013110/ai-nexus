using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Users;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Identity;

public sealed class CurrentUserTests
{
    [Fact]
    public async Task ExistingWindowsUsersAreResolvedWithoutTheIdentityWriteGate()
    {
        await using var factory = new AppFactory();
        using var client = await factory.SignedInAsync();
        var gate = factory.Services.GetRequiredService<IdentityWriteLock>().Gate;
        await gate.WaitAsync();
        try { (await client.GetAsync("/api/v1/me").WaitAsync(TimeSpan.FromSeconds(10))).EnsureSuccessStatusCode(); }
        finally { gate.Release(); }

        // 需要更新最後上線時間的使用者，仍要先通過閘門，之後才會被更新。
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, DateTimeOffset.UtcNow.AddHours(-1)));
        await gate.WaitAsync();
        Task<HttpResponseMessage> pending;
        try
        {
            pending = client.GetAsync("/api/v1/me");
            await Task.Delay(200);
            Assert.False(pending.IsCompleted);
        }
        finally { gate.Release(); }
        (await pending).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(x => x.AdAccount == "alice")).LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task FirstWindowsLoginStoresTheShortAdAccountWithTheDirectoryCase()
    {
        await using var factory = new AppFactory();
        using var client = await factory.SignedInAsync("Carol.Lin");
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(x => x.AdAccount == "carol.lin");
        Assert.Equal("Carol.Lin", user.AdAccount);
        Assert.Equal(UserAccounts.Normalize(" Carol.Lin "), user.AdAccount);
    }

    [Fact]
    public async Task FirstLdapLoginStoresTheShortAdAccountWithoutTheUpnSuffix()
    {
        await using var factory = new AppFactory(ldap: true);
        using var client = factory.CreateClient();
        var me = await Login(client, "bob");
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(x => x.Id == me.Id);
        Assert.Equal("bob", user.AdAccount);
    }

    [Fact]
    public async Task FirstLoginIsRejectedWhenAnotherBoundUserAlreadyHoldsTheAdAccount()
    {
        await using var factory = new AppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { Sid = "S-1-5-21-test-dave-old", DisplayName = "dave", AdAccount = "DAVE", LastSeenAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "dave");
        var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("ad_binding_conflict", await response.Content.ReadAsStringAsync());
        using var check = factory.Services.CreateScope();
        Assert.Equal(1, await check.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync(x => x.AdAccount == "dave"));
    }

    [Fact]
    public async Task ExistingUsersResolveByTheirSidAndFollowTheDirectoryCase()
    {
        await using var factory = new AppFactory();
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existing = new User { Sid = "S-1-5-21-test-erin", AdAccount = "ERIN", DisplayName = "erin", LastSeenAt = DateTimeOffset.UtcNow.AddHours(-1) };
            db.Users.Add(existing);
            await db.SaveChangesAsync();
            id = existing.Id;
        }
        using var client = await factory.SignedInAsync("erin");
        using var check = factory.Services.CreateScope();
        var users = await check.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(x => x.AdAccount == "erin").ToListAsync();
        var user = Assert.Single(users);
        Assert.Equal((id, "erin"), (user.Id, user.AdAccount));
    }
}
