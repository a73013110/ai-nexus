using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using AiNexus.Features.Account;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Persistence;
using AiNexus.Features.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.IntegrationTests.Repositories;

public sealed class RepositoryConnectionTests
{
    [Fact]
    public async Task TokensAreEncryptedAndIsolatedWithPinnedReadOnlyImports()
    {
        var source = new FixtureGitea();
        await using var factory = new NexusFactory(services: services => { services.RemoveAll<IGiteaClient>(); services.AddSingleton<IGiteaClient>(source); services.PostConfigure<GiteaOptions>(o => o.Enabled = true); });
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var owner = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id; const string token = "fixtureOnlyReadTokenForGitea00001";
        var response = await alice.PostAsJsonAsync("/api/v1/repositories/connection", new ConnectRepositoryRequest(token)); response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(token, raw);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var connection = await db.Set<RepositoryConnection>().SingleAsync(); Assert.DoesNotContain(token, connection.ProtectedToken);
            var wrong = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("AiNexus.Gitea.UserToken.v1", Guid.NewGuid().ToString("N"), connection.BaseUrl);
            Assert.Throws<CryptographicException>(() => wrong.Unprotect(connection.ProtectedToken));
        }
        Assert.Equal(HttpStatusCode.Conflict, (await bob.GetAsync("/api/v1/repositories")).StatusCode);
        Assert.Single((await alice.GetFromJsonAsync<RepositoryPageDto>("/api/v1/repositories"))!.Items);
        var tree = (await alice.GetFromJsonAsync<RepositoryTreeDto>("/api/v1/repositories/tree?repository=hanglong/nexus"))!; Assert.Equal(new string('a', 40), tree.Commit);
        var file = (await alice.GetFromJsonAsync<RepositoryFileDto>($"/api/v1/repositories/file?repository=hanglong/nexus&commit={tree.Commit}&path=README.md"))!; Assert.Contains("技術文件", file.Text);
        var create = await alice.PostAsJsonAsync("/api/v1/knowledge/collections", new { name = "程式文件", description = "" }); create.EnsureSuccessStatusCode(); var collection = (await create.Content.ReadFromJsonAsync<CollectionDto>())!;
        var body = new RepositoryImportRequest(file.Repository, file.Commit, file.Path, collection.Resource.Id);
        var imported = await alice.PostAsJsonAsync("/api/v1/repositories/import", body); imported.EnsureSuccessStatusCode(); var document = (await imported.Content.ReadFromJsonAsync<DocumentDto>())!;
        var again = await alice.PostAsJsonAsync("/api/v1/repositories/import", body); again.EnsureSuccessStatusCode(); Assert.Equal(document.Id, (await again.Content.ReadFromJsonAsync<DocumentDto>())!.Id);
        Assert.All(source.Requests, x => Assert.StartsWith("api/v1/", x)); Assert.Contains(source.Requests, x => x.Contains("?ref=" + tree.Commit));
        (await alice.DeleteAsync("/api/v1/repositories/connection")).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.Conflict, (await alice.GetAsync("/api/v1/repositories")).StatusCode);
        using var finalScope = factory.Services.CreateScope(); Assert.Empty(await finalScope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<RepositoryConnection>().ToListAsync());
    }
}
