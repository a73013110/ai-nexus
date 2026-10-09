using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Account;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AiNexus.Tests;

public sealed class RepositoryTests
{
    [Fact]
    public async Task TokensAreEncryptedAndIsolatedWithPinnedReadOnlyImports()
    {
        var source = new FixtureGitea();
        await using var factory = new NexusFactory(backgroundJobs: false, services: services => { services.RemoveAll<IGiteaClient>(); services.AddSingleton<IGiteaClient>(source); services.PostConfigure<GiteaOptions>(o => o.Enabled = true); });
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
    [Theory]
    [InlineData("../private")]
    [InlineData("hanglong/../../private")]
    [InlineData("hanglong/nexus?token=bad")]
    public void RepositoryPathsCannotEscapeTheControlledHost(string repository) => Assert.Throws<ApiException>(() => RepositoryService.RepositoryRoute(repository));
    [Theory]
    [InlineData("../secrets")]
    [InlineData("a/../../secret")]
    [InlineData("/absolute")]
    [InlineData("folder\\file")]
    public void FilePathsRejectTraversal(string path) => Assert.Throws<ApiException>(() => RepositoryService.FilePath(path, false));
    [Fact]
    public void BranchNamesCannotMasqueradeAsPinnedCommits() => Assert.Throws<ApiException>(() => RepositoryService.Commit("main"));
}
internal sealed class FixtureGitea : IGiteaClient
{
    public List<string> Requests { get; } = [];
    public bool Revoked { get; set; }
    public string Diff { get; set; } = "diff --git a/app.cs b/app.cs\n--- a/app.cs\n+++ b/app.cs\n@@ -1 +1 @@\n-old\n+new\n";
    public Task<string> GetTextAsync(string token, string path, CancellationToken ct) { Requests.Add(path); if (Revoked) throw new ApiException(403, "gitea_read_failed", "Revoked."); return Task.FromResult(Diff); }
    public Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct)
    {
        Requests.Add(path); if (Revoked) throw new ApiException(403, "gitea_read_failed", "Revoked."); object response = path switch {
            "api/v1/user" => new { login = "fixture-user" },
            var p when p.StartsWith("api/v1/user/repos?", StringComparison.Ordinal) => new[] { new { full_name = "hanglong/nexus", description = "內部文件", @private = true, default_branch = "main" } },
            var p when p.Contains("/git/commits/") => new { sha = p.Split('/').Last() },
            var p when p.Contains("/commits?") => new[] { new { sha = new string('a', 40), commit = new { message = "Recent commit" } } },
            "api/v1/repos/hanglong/nexus" => new { default_branch = "main" },
            "api/v1/repos/hanglong/nexus/branches/main" => new { commit = new { id = new string('a', 40) } },
            var p when p.StartsWith("api/v1/repos/hanglong/nexus/contents?", StringComparison.Ordinal) => new[] { new { name = "README.md", path = "README.md", type = "file", size = 20 } },
            var p when p.StartsWith("api/v1/repos/hanglong/nexus/contents/README.md?", StringComparison.Ordinal) => new { type = "file", encoding = "base64", size = 20, content = Convert.ToBase64String(Encoding.UTF8.GetBytes("# 技術文件\n唯讀內容")) },
            _ => throw new ApiException(404, "fixture_not_found", "Fixture path missing.") };
        return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(response)));
    }
}
