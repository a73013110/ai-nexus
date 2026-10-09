using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Administration;
using AiNexus.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Features.Audit;

namespace AiNexus.Tests;

public sealed class ActivityAuditTests
{
    [Fact]
    public async Task AuditorsCanInvestigateWithoutManagingAccountsAndRevocationAppliesImmediately()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        using var auditor = await factory.SignedInAsync("bob");
        var target = (await auditor.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/audit/catalog")).StatusCode);
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/auditors", new GroupUpdateRequest("稽核查閱", true, ["audit"]))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/roles/auditor", new RoleUpdateRequest("稽核人員", true, ["auditors"]))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/roles", new UserRolesRequest(["member", "auditor"]))).EnsureSuccessStatusCode();
        var me = (await auditor.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Contains(me.Access.Features, feature => feature.Id == "audit" && feature.Route == "/admin/audit");
        Assert.DoesNotContain(me.Access.Features, feature => feature.Id == "admin");
        var catalog = (await auditor.GetFromJsonAsync<AuditCatalogDto>("/api/v1/admin/audit/catalog"))!;
        Assert.Contains(catalog.Features, feature => feature.Id == "audit");
        Assert.NotEmpty(catalog.Models);
        var rows = (await auditor.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?category=administration"))!;
        Assert.Contains(rows, row => row.Action == "admin.user_roles" && row.ResourceId == target.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/roles", new UserRolesRequest(["administrator"]))).StatusCode);
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/auditors", new GroupUpdateRequest("稽核查閱", true, []))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/admin/audit/catalog")).StatusCode);
    }

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

    [Fact]
    public async Task CategoriesCoverHistoricalActionsAndTraceFiltersKeepPagingAndAuthorization()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); using var member = await factory.SignedInAsync("bob");
        var owner = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        const string trace = "1234567890abcdef1234567890abcdef";
        string[] actions = ["identity.login", "admin.user_roles", "resource.acl.updated", "system.diagnostics.configuration", "billing.price.created",
            "logs.query", "admin.conversation_read", "billing.report.export", "dashboard.platform.read", "integration.record.read", "conversation.created"];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.AuditEvents.AddRange(actions.Select(action => new AuditEvent { OwnerId = owner, Action = action, TraceId = trace, Result = action == "billing.report.export" ? "csv" : "saved" }));
            await db.SaveChangesAsync();
        }
        foreach (var category in AuditCategories.Values)
        {
            var rows = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category={category}&traceId={trace}&result=success"))!;
            Assert.NotEmpty(rows); Assert.All(rows, row => { Assert.Equal(category, row.Category); Assert.Equal(trace, row.TraceId); });
            var before = rows[0].Id;
            var page = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category={category}&traceId={trace}&before={before}"))!;
            Assert.All(page, row => Assert.True(row.Id < before));
        }
        var access = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category=access&traceId={trace}&result=success"))!;
        Assert.Contains(access, row => row.Action == "billing.report.export");
        var conversation = await ChatApiTests.CreateConversation(member);
        var operations = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?category=activity&result=success&search={conversation.Id}"))!;
        var created = Assert.Single(operations);
        Assert.Equal("conversation.created", created.Action); Assert.Equal("completed", created.Result);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit?category=unsupported")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit?traceId=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/v1/admin/audit?category=authentication&traceId={trace}")).StatusCode);
    }

    private static async Task Csrf(HttpClient client)
    {
        var session = (await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;
        client.DefaultRequestHeaders.Remove("X-Nexus-CSRF"); client.DefaultRequestHeaders.Add("X-Nexus-CSRF", session.CsrfToken);
    }
    private static async Task Login(HttpClient client, string account)
    {
        await Csrf(client);
        (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest(account, "fixture-password"))).EnsureSuccessStatusCode();
        await Csrf(client);
    }
}
