using System.Text;
using System.Text.Json;
using AiNexus.Features.Repositories;
using AiNexus.Platform.Errors;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// One private repository, hanglong/nexus, at commit aaaa…: README.md, docs/guide.md, docs/logo.png and src/app.cs; issue #1 and
/// pull request #2 (head aaaa…, merge base bbbb…).
/// </summary>
internal sealed class FixtureGitea : IGiteaClient
{
    public static readonly string Head = new('a', 40);
    public static readonly string Base = new('b', 40);
    private static readonly string Docs = new('d', 40);

    public List<string> Requests { get; } = [];
    public List<(string Path, string Body)> Comments { get; } = [];
    public bool Revoked { get; set; }
    public bool ReadOnly { get; set; }
    public string Diff { get; set; } = "diff --git a/app.cs b/app.cs\n--- a/app.cs\n+++ b/app.cs\n@@ -1 +1 @@\n-old\n+new\n";

    public Task<string?> GetTextAsync(string token, string path, int maxBytes, CancellationToken ct)
    {
        Check(path);
        return Task.FromResult(Encoding.UTF8.GetByteCount(Diff) > maxBytes ? null : Diff);
    }

    public Task<JsonDocument> PostCommentAsync(string token, string path, string body, CancellationToken ct)
    {
        Check(path);
        if (ReadOnly) throw new ExternalServiceException(Error.Forbidden("gitea_forbidden"), "Read-only token.");
        Comments.Add((path, body));
        return Json(new { id = 9, html_url = "https://gitea.hanglong.com.tw/hanglong/nexus/pulls/2#issuecomment-9", body });
    }

    public Task<JsonDocument> GetAsync(string token, string path, CancellationToken ct)
    {
        Check(path);
        const string repo = "api/v1/repos/hanglong/nexus";
        object response = path switch
        {
            "api/v1/user" => new { login = "fixture-user" },
            var p when p.StartsWith("api/v1/repos/search?", StringComparison.Ordinal) => new { ok = true, data = new[] { Repository() } },
            repo => Repository(),
            var p when p.StartsWith(repo + "/branches?", StringComparison.Ordinal) => new[] { new { name = "main", commit = new { id = Head } } },
            var p when p.StartsWith(repo + "/tags?", StringComparison.Ordinal) => new[] { new { name = "v1.0", commit = new { sha = Base } } },
            var p when p.StartsWith(repo + "/git/commits/", StringComparison.Ordinal) => Commit(p[(repo.Length + 13)..].Split('?')[0]),
            var p when p.StartsWith(repo + "/commits?", StringComparison.Ordinal) => new[] { Commit(Head), Commit(Base) },
            var p when p.StartsWith(repo + "/contents?", StringComparison.Ordinal) => new object[]
            {
                new { name = "README.md", path = "README.md", type = "file", size = 20, sha = new string('1', 40) },
                new { name = "docs", path = "docs", type = "dir", size = 0, sha = Docs },
                new { name = "src", path = "src", type = "dir", size = 0, sha = new string('2', 40) },
            },
            var p when p.StartsWith(repo + "/contents/README.md?", StringComparison.Ordinal) => File("# 技術文件\n唯讀內容"),
            var p when p.StartsWith(repo + "/contents/docs/guide.md?", StringComparison.Ordinal) => File("# 指南\n使用 ``` 的範例"),
            var p when p.StartsWith(repo + "/contents/docs/logo.png?", StringComparison.Ordinal) => new { type = "file", encoding = "base64", size = 4, content = Convert.ToBase64String([0x89, 0x50, 0x00, 0xFF]) },
            var p when p.StartsWith(repo + "/contents/src/app.cs?", StringComparison.Ordinal) => File("class App { }"),
            var p when p.StartsWith(repo + "/git/trees/" + Docs + "?", StringComparison.Ordinal) => new
            {
                truncated = false,
                tree = new object[]
                {
                    new { path = "guide.md", type = "blob", size = 20 },
                    new { path = "logo.png", type = "blob", size = 4 },
                    new { path = "images", type = "tree", size = 0 },
                },
            },
            var p when p.StartsWith(repo + "/issues?", StringComparison.Ordinal) => new[] { Issue() },
            repo + "/issues/1" => Issue(),
            var p when p.EndsWith("/comments", StringComparison.Ordinal) => new[] { new { id = 7, user = new { login = "reviewer" }, body = "請補上測試", created_at = "2026-10-01T08:00:00Z" } },
            var p when p.StartsWith(repo + "/pulls?", StringComparison.Ordinal) => new[] { Pull() },
            repo + "/pulls/2" => Pull(),
            _ => throw new ExternalServiceException(Error.NotFound("gitea_not_found"), "Fixture path missing."),
        };
        return Json(response);
    }

    private void Check(string path)
    {
        Requests.Add(path);
        if (Revoked) throw new ExternalServiceException(Error.Forbidden("gitea_unauthorized"), "Revoked.");
    }

    private static Task<JsonDocument> Json(object value) => Task.FromResult(JsonDocument.Parse(JsonSerializer.Serialize(value)));

    private static object Repository() => new
    {
        full_name = "hanglong/nexus", description = "內部文件", @private = true, empty = false, default_branch = "main", language = "C#",
        stars_count = 3, open_issues_count = 1, open_pr_counter = 1, updated_at = "2026-10-01T08:00:00Z",
    };

    private static object Commit(string sha) => new
    {
        sha, commit = new { message = "修正登入流程\n\n補上逾時處理", author = new { name = "Fixture", date = "2026-10-01T08:00:00Z" } },
        author = new { login = "fixture-user" }, parents = new[] { new { sha = Base } },
    };

    private static object File(string text) => new { type = "file", encoding = "base64", size = Encoding.UTF8.GetByteCount(text), content = Convert.ToBase64String(Encoding.UTF8.GetBytes(text)) };

    private static object Issue() => new
    {
        number = 1, title = "登入逾時", body = "使用者回報登入逾時。", state = "open", user = new { login = "reporter" },
        labels = new[] { new { name = "bug", color = "ee0701" } }, assignees = Array.Empty<object>(), comments = 1, updated_at = "2026-10-01T08:00:00Z",
    };

    private static object Pull() => new
    {
        number = 2, title = "修正登入逾時", body = "處理 #1", state = "open", merged = false, user = new { login = "fixture-user" }, comments = 1,
        head = new { @ref = "fix/login", sha = Head }, @base = new { @ref = "main", sha = new string('c', 40) }, merge_base = Base, updated_at = "2026-10-01T08:00:00Z",
    };
}
