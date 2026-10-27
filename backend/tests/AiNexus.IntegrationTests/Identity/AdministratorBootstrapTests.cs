using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Admin.Identity.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Identity;

public sealed class AdministratorBootstrapTests
{
    [Fact]
    public async Task BootstrapGrantsOnlyConfiguredAccountsAndDoesNotRestoreRevokedPrivileges()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var me = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Contains(me.Access.Features, x => x.Id == "identity.users"); Assert.DoesNotContain((await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "identity.users");
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Remove(await db.Set<UserRole>().SingleAsync(x => x.UserId == me.Id && x.RoleId == "administrator")); await db.SaveChangesAsync();
        }
        using var again = await factory.SignedInAsync(); Assert.Equal(HttpStatusCode.Forbidden, (await again.GetAsync("/api/v1/admin/catalog")).StatusCode);
        using var check = factory.Services.CreateScope(); Assert.Equal(1, await check.ServiceProvider.GetRequiredService<AppDbContext>().AuditEvents.CountAsync(x => x.Action == "identity.administrator.granted"));
    }

    [Fact]
    public async Task LocalNameMatchingBootstrapDoesNotGrantAdministration()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice", "tester"]);
        using var admin = f.CreateClient(); await Login(admin, "alice"); await CreateUser(admin, LocalAccount());
        using var local = f.CreateClient(); var me = await Login(local, "tester", "local");
        Assert.DoesNotContain(me.Access.Features, x => x.Id == "identity.users");
        Assert.Equal(HttpStatusCode.Forbidden, (await local.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(Guid.NewGuid(), "檢查權限"))).StatusCode);
    }
}
