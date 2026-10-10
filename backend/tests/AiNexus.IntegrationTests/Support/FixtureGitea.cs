using System.Text;
using System.Text.Json;
using AiNexus.Features.Repositories;
using AiNexus.Platform.Errors;

namespace AiNexus.IntegrationTests.Support;

internal sealed class FixtureGitea : IGiteaClient
{
    public List<string> Requests { get; } = [];
    public bool Revoked { get; set; }
    public string Diff { get; set; } = "diff --git a/app.cs b/app.cs\n--- a/app.cs\n+++ b/app.cs\n@@ -1 +1 @@\n-old\n+new\n";
    public Task<string> GetTextAsync(string token, string path, CancellationToken ct) { Requests.Add(path); if (Revoked) throw new ExternalServiceException(Error.Forbidden("gitea_read_failed"), "Revoked."); return Task.FromResult(Diff); }
    public Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct)
    {
        Requests.Add(path); if (Revoked) throw new ExternalServiceException(Error.Forbidden("gitea_read_failed"), "Revoked."); object response = path switch {
            "api/v1/user" => new { login = "fixture-user" },
            var p when p.StartsWith("api/v1/user/repos?", StringComparison.Ordinal) => new[] { new { full_name = "hanglong/nexus", description = "內部文件", @private = true, default_branch = "main" } },
            var p when p.Contains("/git/commits/") => new { sha = p.Split('/').Last() },
            var p when p.Contains("/commits?") => new[] { new { sha = new string('a', 40), commit = new { message = "Recent commit" } } },
            "api/v1/repos/hanglong/nexus" => new { default_branch = "main" },
            "api/v1/repos/hanglong/nexus/branches/main" => new { commit = new { id = new string('a', 40) } },
            var p when p.StartsWith("api/v1/repos/hanglong/nexus/contents?", StringComparison.Ordinal) => new[] { new { name = "README.md", path = "README.md", type = "file", size = 20 } },
            var p when p.StartsWith("api/v1/repos/hanglong/nexus/contents/README.md?", StringComparison.Ordinal) => new { type = "file", encoding = "base64", size = 20, content = Convert.ToBase64String(Encoding.UTF8.GetBytes("# 技術文件\n唯讀內容")) },
            _ => throw new ExternalServiceException(Error.NotFound("fixture_not_found"), "Fixture path missing.") };
        return Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(response)));
    }
}
