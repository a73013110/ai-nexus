using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Work.Conversations;
using Microsoft.Extensions.Time.Testing;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Identity;

public sealed class SessionLockTests
{
    private static readonly TimeSpan JustPastIdle = TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1));

    private static async Task<(AppFactory Factory, FakeTimeProvider Clock, HttpClient Client)> SignedInAsync()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var factory = new AppFactory(ldap: true, clock: clock);
        var client = factory.CreateClient();
        await Login(client, "alice");
        return (factory, clock, client);
    }

    private static async Task<AuthSessionDto> SessionAsync(HttpClient client) => (await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;

    private static async Task AssertLockedAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("session_locked", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IdleSessionLocksKeepsItsIdentityAndUnlocksWithThePassword()
    {
        var (factory, clock, client) = await SignedInAsync();
        await using var _ = factory; using var __ = client;
        var token = client.DefaultRequestHeaders.GetValues("X-CSRF-Token").Single();
        clock.Advance(JustPastIdle);
        await AssertLockedAsync(client);
        var locked = await SessionAsync(client);
        Assert.True(locked.Authenticated);
        Assert.Equal(SessionTicket.Idle, locked.Lifetime!.LockedBy);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/auth/activity", null)).StatusCode);

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/unlock", new UnlockSessionRequest("wrong"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        await AssertLockedAsync(client);

        (await client.PostAsJsonAsync("/api/v1/auth/unlock", new UnlockSessionRequest("fixture-password"))).EnsureSuccessStatusCode();
        Assert.Null((await SessionAsync(client)).Lifetime!.LockedBy);
        // 解鎖前的 CSRF token 繼續有效，鎖定時暫存的寫入才能直接重送。
        client.DefaultRequestHeaders.Remove("X-CSRF-Token"); client.DefaultRequestHeaders.Add("X-CSRF-Token", token);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/conversations", new CreateConversationRequest())).StatusCode);
    }

    [Fact]
    public async Task OnlyReportedActivityPostponesTheIdleLock()
    {
        var (factory, clock, client) = await SignedInAsync();
        await using var _ = factory; using var __ = client;
        clock.Advance(TimeSpan.FromMinutes(20));
        // 一般請求（輪詢）不延後。
        (await client.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        var reported = await client.PostAsync("/api/v1/auth/activity", null);
        reported.EnsureSuccessStatusCode();
        Assert.Equal(1800, (await reported.Content.ReadFromJsonAsync<AuthSessionDto>())!.Lifetime!.SecondsUntilLock);

        clock.Advance(TimeSpan.FromMinutes(20));
        (await client.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        clock.Advance(TimeSpan.FromMinutes(11));
        await AssertLockedAsync(client);
    }

    [Fact]
    public async Task ReauthenticationLimitLocksEvenWhileActive()
    {
        var (factory, clock, client) = await SignedInAsync();
        await using var _ = factory; using var __ = client;
        for (var hour = 0; hour < 8; hour++)
        {
            for (var step = 0; step < 3; step++)
            {
                clock.Advance(TimeSpan.FromMinutes(20));
                if (hour == 7 && step == 2) break;
                (await client.PostAsync("/api/v1/auth/activity", null)).EnsureSuccessStatusCode();
            }
        }
        await AssertLockedAsync(client);
        Assert.Equal(SessionTicket.Reauthentication, (await SessionAsync(client)).Lifetime!.LockedBy);
    }

    [Fact]
    public async Task HardLimitEndsTheSessionAndUnlockRestartsIt()
    {
        var (factory, clock, client) = await SignedInAsync();
        await using var _ = factory; using var __ = client;
        clock.Advance(TimeSpan.FromHours(20));
        (await client.PostAsJsonAsync("/api/v1/auth/unlock", new UnlockSessionRequest("fixture-password"))).EnsureSuccessStatusCode();
        // 解鎖重新起算：距登入已超過 24 小時仍有效。
        clock.Advance(TimeSpan.FromHours(5));
        Assert.True((await SessionAsync(client)).Authenticated);
        clock.Advance(TimeSpan.FromHours(19).Add(TimeSpan.FromSeconds(1)));
        Assert.False((await SessionAsync(client)).Authenticated);
        Assert.DoesNotContain("session_locked", await (await client.GetAsync("/api/v1/me")).Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LockedSessionCanSignOutForAnotherAccount()
    {
        var (factory, clock, client) = await SignedInAsync();
        await using var _ = factory; using var __ = client;
        clock.Advance(JustPastIdle);
        (await client.PostAsync("/api/v1/auth/logout", null)).EnsureSuccessStatusCode();
        Assert.False((await SessionAsync(client)).Authenticated);
    }

    [Fact]
    public async Task AnIdleTestIdentityReturnsToTheAdministratorWhoUnlocksWithTheirOwnPassword()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(ldap: true, administrators: ["alice"], clock: clock);
        using var admin = factory.CreateClient();
        var actor = await Login(admin, "alice");
        var target = await CreateUser(admin, LocalAccount());
        (await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(target, "確認閒置鎖定"))).EnsureSuccessStatusCode();
        await Csrf(admin);
        // 測試身分不能解鎖：輸入的是被測者的密碼，失敗次數會算到對方頭上。
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/auth/unlock", new UnlockSessionRequest("fixture-password"))).StatusCode);
        clock.Advance(JustPastIdle);

        var restored = await SessionAsync(admin);
        Assert.Null(restored.Testing);
        Assert.Equal(actor.Id, restored.UserId);
        Assert.Equal(SessionTicket.Idle, restored.Lifetime!.LockedBy);
        await AssertLockedAsync(admin);

        admin.DefaultRequestHeaders.Remove("X-CSRF-Token"); admin.DefaultRequestHeaders.Add("X-CSRF-Token", restored.CsrfToken);
        (await admin.PostAsJsonAsync("/api/v1/auth/unlock", new UnlockSessionRequest("fixture-password"))).EnsureSuccessStatusCode();
        (await admin.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
    }
}
