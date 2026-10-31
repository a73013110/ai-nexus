using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Users;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
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
        // 過了同步間隔、但目錄資料沒變的使用者只刷新驗證時間，也不必等閘門。
        await SetAliceAsync(factory, p => p.SetProperty(x => x.LastSeenAt, DateTimeOffset.UtcNow.AddHours(-1)));
        await gate.WaitAsync();
        try
        {
            (await client.GetAsync("/api/v1/me").WaitAsync(TimeSpan.FromSeconds(10))).EnsureSuccessStatusCode();
            (await client.GetAsync("/api/v1/me").WaitAsync(TimeSpan.FromSeconds(10))).EnsureSuccessStatusCode();
        }
        finally { gate.Release(); }
        Assert.True((await AliceAsync(factory)).LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(-1));

        // 欄位要跟著目錄改的使用者，仍要先通過閘門才會更新。
        await SetAliceAsync(factory, p => p.SetProperty(x => x.DisplayName, "舊名"));
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
        Assert.Equal("alice", (await AliceAsync(factory)).DisplayName);
    }

    [Fact]
    public async Task WindowsUsersLookUpTheDirectoryOnlyWhenDueForSync()
    {
        var directory = new FixtureProfiles(new("Erin Chen", "erin@fixture.test"));
        await using var factory = new AppFactory(seed: db => db.Users.Add(Erin(DateTimeOffset.UtcNow)), services: s => s.AddSingleton<IDirectoryProfileLookup>(directory));
        using var client = WindowsClient(factory, "erin");
        (await client.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        Assert.Equal(0, directory.Calls);
        Assert.Equal("erin", (await UserAsync(factory, "erin")).DisplayName);

        await SetUserAsync(factory, "erin", p => p.SetProperty(x => x.LastSeenAt, DateTimeOffset.UtcNow.AddHours(-1)));
        (await client.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        Assert.Equal(1, directory.Calls);
        var user = await UserAsync(factory, "erin");
        Assert.Equal(("Erin Chen", "erin@fixture.test", EmailSources.Directory), (user.DisplayName, user.Email, user.EmailSource));
    }

    [Fact]
    public async Task UnavailableDirectoryLeavesTheNameAndEmailUnchanged()
    {
        var verified = DateTimeOffset.UtcNow.AddDays(-1);
        await using var factory = new AppFactory(seed: db =>
        {
            var erin = Erin(verified);
            erin.DisplayName = "Erin Chen"; erin.Email = "erin@fixture.test"; erin.EmailSource = EmailSources.Directory; erin.EmailVerifiedAt = verified;
            db.Users.Add(erin);
        }, services: s => s.AddSingleton<IDirectoryProfileLookup>(new FixtureProfiles(null)));
        using var client = WindowsClient(factory, "erin");
        (await client.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        var user = await UserAsync(factory, "erin");
        Assert.Equal(("Erin Chen", "erin@fixture.test", (DateTimeOffset?)verified), (user.DisplayName, user.Email, user.EmailVerifiedAt));
        Assert.True(user.DirectoryVerifiedAt > verified);

        // 新使用者在目錄不可用時仍能登入，先以帳號名建立。
        using var frank = WindowsClient(factory, "frank");
        (await frank.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        Assert.Equal("frank", (await UserAsync(factory, "frank")).DisplayName);
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

    private static User Erin(DateTimeOffset seen) => new() { Sid = "S-1-5-21-test-erin", AdAccount = "erin", DisplayName = "erin", LastSeenAt = seen, DirectoryVerifiedAt = seen };

    private static HttpClient WindowsClient(AppFactory factory, string account)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", account);
        client.DefaultRequestHeaders.Add("X-Test-Profile", "none");
        return client;
    }

    private static Task<User> AliceAsync(AppFactory factory) => UserAsync(factory, "alice");

    private static async Task<User> UserAsync(AppFactory factory, string adAccount)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.AdAccount == adAccount);
    }

    private static Task SetAliceAsync(AppFactory factory, Action<UpdateSettersBuilder<User>> set) => SetUserAsync(factory, "alice", set);

    private static async Task SetUserAsync(AppFactory factory, string adAccount, Action<UpdateSettersBuilder<User>> set)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(x => x.AdAccount == adAccount).ExecuteUpdateAsync(set);
    }

    private sealed class FixtureProfiles(DirectoryProfile? profile) : IDirectoryProfileLookup
    {
        public int Calls { get; private set; }

        public DirectoryProfile? Find(string sid, string account)
        {
            Calls++;
            return profile;
        }
    }
}
