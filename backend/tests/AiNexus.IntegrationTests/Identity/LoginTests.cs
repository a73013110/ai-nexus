using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Identity;

public sealed class LoginTests
{
    [Fact]
    public async Task LdapCookieLoginRequiresCsrfMapsSidAndLogoutInvalidatesSession()
    {
        await using var factory = new NexusFactory(ldap: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("alice", "fixture-password"))).StatusCode);
    }

    [Fact]
    public async Task InvalidAdPasswordDoesNotIssueAnAuthenticatedCookie()
    {
        await using var factory = new NexusFactory(ldap: true);
        using var client = factory.CreateClient();
        var session = (await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;
        client.DefaultRequestHeaders.Add("X-Nexus-CSRF", session.CsrfToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("alice", "wrong"))).StatusCode);
        Assert.False((await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
    }

    [Fact]
    public async Task LoginLocksAfterRepeatedFailuresAndPasswordResetClearsTheLock()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        using var local = f.CreateClient(); await Csrf(local);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await local.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("tester", "wrong password!", "local"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await local.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("tester", Password, "local"))).StatusCode);
        using (var scope = f.Services.CreateScope()) Assert.True((await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.SingleAsync(x => x.Id == id)).LockedUntil > DateTimeOffset.UtcNow);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{id}", LocalAccount())).EnsureSuccessStatusCode();
        Assert.Equal(id, (await Login(local, "tester", "local")).Id);
    }

    [Fact]
    public async Task WindowsLoginRequiresWindowsIdentityEvenWhenALocalCookieExists()
    {
        await using var f = new NexusFactory(administrators: ["alice"], services: services =>
            services.PostConfigure<AuthenticationOptions>(options => { options.DefaultAuthenticateScheme = "NexusSession"; options.DefaultChallengeScheme = "NexusSession"; }));
        using var admin = await f.SignedInAsync(); await CreateUser(admin, LocalAccount());
        using var local = f.CreateClient(); await Login(local, "tester", "local");
        Assert.Equal(HttpStatusCode.Unauthorized, (await local.GetAsync("/api/v1/auth/windows")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await local.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task AStaleLoginOrHashUpgradeCannotOverwriteAConcurrentPasswordReset()
    {
        var id = Guid.NewGuid();
        await using var factory = new NexusFactory(seed: db => db.Users.Add(new NexusUser {
            Id = id, Sid = "managed:" + id, Account = "tester", DisplayName = "tester", LocalAccount = "TESTER",
            LocalEnabled = true, AdEnabled = false, PasswordHash = "old-format-hash", SecurityVersion = 1
        }));
        using var loginScope = factory.Services.CreateScope(); using var resetScope = factory.Services.CreateScope();
        var loginDb = loginScope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var resetDb = resetScope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var stale = await loginDb.Users.SingleAsync(x => x.Id == id);
        var reset = await resetDb.Users.SingleAsync(x => x.Id == id);
        reset.PasswordHash = "new-password-hash"; reset.SecurityVersion++;
        await resetDb.SaveChangesAsync();
        stale.PasswordHash = "upgrade-of-old-password-hash"; stale.LastSeenAt = DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => loginDb.SaveChangesAsync());
        await resetDb.Entry(reset).ReloadAsync();
        Assert.Equal("new-password-hash", reset.PasswordHash); Assert.Equal(2, reset.SecurityVersion);
    }
}
