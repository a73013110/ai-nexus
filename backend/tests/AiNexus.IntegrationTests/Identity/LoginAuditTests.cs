using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Administration.Users;
using AiNexus.Features.Audit;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Identity;

public sealed class LoginAuditTests
{
    [Fact]
    public async Task WindowsHandshakeChallengesAreNotLoginFailuresButExplicitLoginAndWorkspaceDenialAreAudited()
    {
        await using var factory = new NexusFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/auth/windows")).StatusCode);
        using var client = await factory.SignedInAsync();
        (await client.GetAsync("/api/v1/auth/windows")).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var user = await db.Users.SingleAsync();
            var success = await db.AuditEvents.SingleAsync(x => x.Action == "identity.login");
            Assert.Equal(user.Id, success.ActorId); Assert.Equal("success", success.Result);
            Assert.Contains("windows", success.DetailsJson!);
            user.AdEnabled = false; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/auth/windows")).StatusCode);
        using var check = factory.Services.CreateScope();
        var rows = await check.ServiceProvider.GetRequiredService<NexusDbContext>().AuditEvents.Where(x => x.Action == "identity.login").ToListAsync();
        Assert.Equal(2, rows.Count); Assert.Single(rows, row => row.Result == "failed" && row.IssueCode != null);
    }

    [Fact]
    public async Task MissingDurableLoginAuditPreventsAnAuthenticatedCookie()
    {
        await using var factory = new NexusFactory(ldap: true, services: services =>
            services.AddDbContext<NexusDbContext>(options => options.AddInterceptors(new RejectLoginAudit())));
        using var client = factory.CreateClient(); await Csrf(client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("alice", "fixture-password"))).StatusCode);
        Assert.False((await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
    }

    private sealed class RejectLoginAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditEvent>().Any(entry => entry.State == EntityState.Added && entry.Entity.Action == "identity.login"))
                throw new DbUpdateException("Fixture audit store unavailable");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task LoginFailuresStayUnverifiedAndCorrelateWithThePublicProblemWithoutRecordingPasswords()
    {
        await using var factory = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var client = factory.CreateClient();
        await Login(client, "alice");
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        var denied = await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("unverified-account", "never-persist-this-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var problem = (await denied.Content.ReadFromJsonAsync<SafeProblemDetails>())!;
        Assert.True((await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
        var rows = (await client.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?category=authentication&action=identity.login"))!;
        var failure = Assert.Single(rows, x => x.Result == "failed");
        Assert.Equal("未驗證", failure.Actor);
        Assert.Null(failure.ResourceId); Assert.Null(failure.ActingAs);
        Assert.Equal(problem.IssueCode, failure.IssueCode);
        Assert.Matches("^[a-f0-9]{32}$", failure.TraceId!); Assert.NotNull(failure.OperationId);
        Assert.Contains("unverified-account", failure.DetailsJson!);
        Assert.DoesNotContain("never-persist-this-password", failure.DetailsJson!);
        Assert.DoesNotContain("Password", failure.DetailsJson!, StringComparison.OrdinalIgnoreCase);
        var success = Assert.Single(rows, x => x.Result == "success");
        Assert.Equal(me.Id, success.ResourceId); Assert.Equal(me.Account, success.Actor);
        Assert.Equal("authentication", success.Category);
        // Routine session refreshes do not become another login event.
        await client.GetAsync("/api/v1/auth/session"); await client.GetAsync("/api/v1/me");
        var refreshed = (await client.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=identity.login"))!;
        Assert.Equal(2, refreshed.Length);
        (await client.PostAsync("/api/v1/auth/logout", null)).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var logout = await scope.ServiceProvider.GetRequiredService<NexusDbContext>().AuditEvents.SingleAsync(x => x.Action == "identity.logout");
        Assert.Equal(me.Id, logout.ActorId); Assert.Equal("completed", logout.Result);
        Assert.False((await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
    }

    [Fact]
    public async Task LocalLockoutUpdatesAndRejectedLoginAuditAreBothDurable()
    {
        await using var factory = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = factory.CreateClient(); await Login(admin, "alice");
        var created = await admin.PostAsJsonAsync("/api/v1/admin/users", new UserAccountRequest("本地同事", true, false, true, null, "audit-local", ["member"], "Fixture-local-password-123!"));
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<CreatedUserDto>())!.Id;
        using var client = factory.CreateClient(); await Csrf(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("audit-local", "incorrect-password", "local"))).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.Equal(1, (await db.Users.SingleAsync(x => x.Id == id)).FailedLogins);
        var failure = await db.AuditEvents.SingleAsync(x => x.Action == "identity.login" && x.Result == "failed");
        Assert.Equal(Guid.Empty, failure.ActorId); Assert.Equal(Guid.Empty, failure.OwnerId);
        Assert.Contains("local", failure.DetailsJson!); Assert.DoesNotContain("incorrect-password", failure.DetailsJson!);
    }
}
