using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Identity;

public sealed class SessionTests
{
    [Fact]
    public async Task LdapSessionAndCsrfAreIsolatedBetweenAccountsAndSupportLogout()
    {
        await using var factory = new NexusFactory(ldap: true);
        using var alice = factory.CreateClient(); using var bob = factory.CreateClient();
        await Login(alice, "alice"); await Login(bob, "bob");
        var me = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal("alice@fixture.test", me.Account);
        var conversation = await ChatApi.CreateConversation(alice);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.False((await alice.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task AnonymousAndUntrustedIdentityHeaderAreRejected()
    {
        await using var factory = new NexusFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User", "administrator");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/conversations")).StatusCode);
    }

    [Fact]
    public async Task WritesRequireCsrfBoundToTheSignedInUser()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var token = alice.DefaultRequestHeaders.GetValues("X-Nexus-CSRF").Single();
        bob.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        bob.DefaultRequestHeaders.Add("X-Nexus-CSRF", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync("/api/v1/conversations", new CreateConversationRequest())).StatusCode);
        alice.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsJsonAsync("/api/v1/conversations", new CreateConversationRequest())).StatusCode);
    }

    [Fact]
    public async Task LegacyAdCookiesKeepTheirLoginMethodAndAreRevokedByPolicyChanges()
    {
        await using var f = new NexusFactory(ldap: true);
        using var modern = f.CreateClient(); var me = await Login(modern, "alice");
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == me.Id);
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([
            new(System.Security.Claims.ClaimTypes.PrimarySid, user.Sid), new(System.Security.Claims.ClaimTypes.Name, user.Account)
        ], AuthEndpoints.CookieScheme));
        using var legacy = CookieClient(f, principal);
        var session = (await legacy.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;
        Assert.True(session.Authenticated); Assert.Equal("ad", session.Method);
        user.AdEnabled = false; user.SecurityVersion++; await db.SaveChangesAsync();
        Assert.False((await legacy.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
    }

    [Fact]
    public async Task CookieRequestsReadTheSignedInUserOnce()
    {
        await using var factory = new NexusFactory(ldap: true, services: RequestQueries.Register);
        using var client = factory.CreateClient();
        var session = (await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;
        client.DefaultRequestHeaders.Add("X-Nexus-CSRF", session.CsrfToken);
        (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("alice", "fixture-password"))).EnsureSuccessStatusCode();
        var queries = factory.Services.GetRequiredService<RequestQueries>(); queries.Clear();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal("alice", me.Account.Split('@')[0]);
        // Cookie validation reads the row; the current user reuses it instead of a second single-row read.
        Assert.Equal(1, queries.Count("/api/v1/me", "FROM \"Users\" AS", "LIMIT 2"));
    }
}
