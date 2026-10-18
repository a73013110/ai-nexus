using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AiNexus.Features.Work.Repositories;
using AiNexus.Features.Work.Repositories.Browsing;
using AiNexus.Features.Work.Repositories.Connections;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.IntegrationTests.Repositories;

public sealed class RepositoryConnectionTests
{
    internal const string Token = "fixtureOnlyReadTokenForGitea00001";

    internal static AppFactory Factory(FixtureGitea source, Action<IServiceCollection>? more = null) => new(services: services =>
    {
        services.RemoveAll<IGiteaClient>(); services.AddSingleton<IGiteaClient>(source); services.PostConfigure<GiteaOptions>(o => o.Enabled = true);
        more?.Invoke(services);
    });

    internal static async Task ConnectAsync(HttpClient client) =>
        (await client.PostAsJsonAsync("/api/v1/repositories/connection", new ConnectRepositoryRequest(Token))).EnsureSuccessStatusCode();

    [Fact]
    public async Task TokensAreEncryptedPerUserAndHostAndDisconnectingStopsReads()
    {
        var source = new FixtureGitea();
        await using var factory = Factory(source);
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var response = await alice.PostAsJsonAsync("/api/v1/repositories/connection", new ConnectRepositoryRequest(Token)); response.EnsureSuccessStatusCode();
        Assert.DoesNotContain(Token, await response.Content.ReadAsStringAsync());
        var status = (await response.Content.ReadFromJsonAsync<RepositoryStatusDto>())!; Assert.True(status.Connected); Assert.Equal("fixture-user", status.Login);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var connection = await db.Set<RepositoryConnection>().SingleAsync(); Assert.DoesNotContain(Token, connection.ProtectedToken);
            var wrong = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("AiNexus.Gitea.UserToken.v1", Guid.NewGuid().ToString("N"), connection.BaseUrl);
            Assert.Throws<CryptographicException>(() => wrong.Unprotect(connection.ProtectedToken));
        }
        Assert.Equal(HttpStatusCode.Conflict, (await bob.GetAsync("/api/v1/repositories")).StatusCode);
        Assert.Single((await alice.GetFromJsonAsync<RepositoryPageDto>("/api/v1/repositories"))!.Items);
        Assert.All(source.Requests, x => Assert.StartsWith("api/v1/", x));
        (await alice.DeleteAsync("/api/v1/repositories/connection")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await alice.GetAsync("/api/v1/repositories")).StatusCode);
        using var finalScope = factory.Services.CreateScope(); Assert.Empty(await finalScope.ServiceProvider.GetRequiredService<AppDbContext>().Set<RepositoryConnection>().ToListAsync());
    }

    [Fact]
    public async Task RejectedTokensReportAReviewedReason()
    {
        await using var factory = Factory(new FixtureGitea { Revoked = true });
        using var client = await factory.SignedInAsync();
        var response = await client.PostAsJsonAsync("/api/v1/repositories/connection", new ConnectRepositoryRequest(Token));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Contains("gitea_unauthorized", await response.Content.ReadAsStringAsync());
    }
}
