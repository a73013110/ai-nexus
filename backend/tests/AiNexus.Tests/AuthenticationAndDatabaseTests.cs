using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Persistence;
using AiNexus.Features.Identity;
using AiNexus.Platform.Data;
using EDoc.Core.Database.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiNexus.Tests;

public sealed class AuthenticationAndDatabaseTests
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
    public async Task LdapSessionAndCsrfAreIsolatedBetweenAccountsAndSupportLogout()
    {
        await using var factory = new NexusFactory(ldap: true);
        using var alice = factory.CreateClient(); using var bob = factory.CreateClient();
        await Login(alice, "alice"); await Login(bob, "bob");
        var me = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal("alice@fixture.test", me.Account);
        var conversation = await ChatApiTests.CreateConversation(alice);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.False((await alice.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/api/v1/me")).StatusCode);
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

    private static async Task Login(HttpClient client, string account)
    {
        var session = (await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;
        client.DefaultRequestHeaders.Add("X-Nexus-CSRF", session.CsrfToken);
        (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest(account, "fixture-password"))).EnsureSuccessStatusCode();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        client.DefaultRequestHeaders.Remove("X-Nexus-CSRF"); client.DefaultRequestHeaders.Add("X-Nexus-CSRF", me.CsrfToken);
    }

    [Fact]
    public void LdapFiltersEscapeInjectionCharacters()
        => Assert.Equal(@"a\2a\29\28\5cb\00", LdapAuthenticator.EscapeFilter("a*)(\\b\0"));

    [Fact]
    public async Task OriginalDbHelperBindsParametersAndRollsBackUncommittedTransactions()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync();
        using var scope = factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IDbHelper<INexusDatabase>>();
        Assert.Equal("'; DROP TABLE Users;--", await helper.QuerySingleAsync<string>("SELECT @Value", new { Value = "'; DROP TABLE Users;--" }));
        await helper.ExecuteAsync("CREATE TABLE HelperProbe (Value TEXT NOT NULL)");
        using (var transaction = await helper.BeginTransactionAsync()) await transaction.ExecuteAsync("INSERT INTO HelperProbe VALUES (@Value)", new { Value = "rollback" });
        Assert.Equal(0, await helper.QuerySingleAsync<int>("SELECT COUNT(*) FROM HelperProbe"));
    }

    [Fact]
    public void DatabaseCredentialsUseConnectionStringBuilderAndRemainOutOfErrors()
    {
        var config = new ConfigurationManager();
        config["Database:Server"] = "sql.fixture.test";
        config["Database:User"] = "login"; config["Database:Password"] = "fixture;\"password";
        LocalDatabaseSettings.Apply(config);
        var connection = new SqlConnectionStringBuilder(config.GetConnectionString("Nexus"));
        Assert.Equal("AiNexus", connection.InitialCatalog); Assert.Equal("sql.fixture.test", connection.DataSource);
        Assert.Equal("fixture;\"password", connection.Password); Assert.False(connection.PersistSecurityInfo);
    }

    [Fact]
    public void ExplicitSelfSignedSqlCertificatesRemainEncryptedInProduction()
    {
        var config = new ConfigurationManager(); config["Database:User"] = "fixture"; config["Database:Password"] = "fixture"; config["Database:TrustServerCertificate"] = "true";
        LocalDatabaseSettings.Apply(config);
        var connection = new SqlConnectionStringBuilder(config.GetConnectionString("Nexus"));
        Assert.True(connection.TrustServerCertificate);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, connection.Encrypt);
    }

    [Theory]
    [InlineData("Encrypt=False;TrustServerCertificate=False")]
    [InlineData("Encrypt=False;TrustServerCertificate=True")]
    public void FullConnectionStringCannotBypassProductionTls(string settings)
    {
        var config = new ConfigurationManager();
        config["ConnectionStrings:Nexus"] = "Server=fixture;Database=AiNexus;User ID=fixture;Password=fixture;" + settings;
        Assert.Throws<InvalidOperationException>(() => LocalDatabaseSettings.Apply(config));
    }
}
