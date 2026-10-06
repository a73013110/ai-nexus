using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Knowledge;
using AiNexus.Modules.Operations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Repositories;

public sealed class RepositoryService(NexusDbContext db, IGiteaClient client, IDataProtectionProvider protection,
    IOptions<GiteaOptions> options, AttachmentService attachments, DocumentService documents, ResourceAccess access, RepositoryWriteLock writes)
{
    private string Host => new Uri(options.Value.BaseUrl.TrimEnd('/') + "/").AbsoluteUri;
    public string BaseUrl => Host;
    public async Task RequireCommitAsync(Guid owner, string repo, string commit, CancellationToken ct)
    {
        Commit(commit);
        using var metadata = await client.GetAsync(await TokenAsync(owner, ct), RepositoryRoute(repo) + "/git/commits/" + commit, ct);
    }
    public async Task<string> DiffAsync(Guid owner, string repo, string head, string? basis, CancellationToken ct)
    {
        Commit(head); if (basis is not null) Commit(basis);
        var route = RepositoryRoute(repo);
        var path = basis is null ? route + "/git/commits/" + head + ".diff" : route + "/compare/" + basis + ".." + head + "?output=diff";
        var diff = await client.GetTextAsync(await TokenAsync(owner, ct), path, ct);
        if (!string.IsNullOrWhiteSpace(diff) && !diff.TrimStart().StartsWith("diff --git ", StringComparison.Ordinal))
            throw new ApiException(409, "gitea_diff_unsupported", "Gitea 未回傳完整的 diff。區間 review 需要支援 compare output=diff 的 Gitea 版本。");
        return diff;
    }
    public async Task<IReadOnlyList<RepositoryCommitDto>> CommitsAsync(Guid owner, string repo, CancellationToken ct)
    {
        using var json = await client.GetAsync(await TokenAsync(owner, ct), RepositoryRoute(repo) + "/commits?limit=30", ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array) throw new ApiException(502, "gitea_response_invalid", "Gitea 回應格式不正確。");
        return json.RootElement.EnumerateArray().Take(30).Select(x => new RepositoryCommitDto(Text(x, "sha", 64), Text(x.GetProperty("commit"), "message", 500))).ToArray();
    }
    private IDataProtector Protector(Guid owner) => protection.CreateProtector("AiNexus.Gitea.UserToken.v1", owner.ToString("N"), Host);
    public async Task<RepositoryStatusDto> StatusAsync(Guid owner, CancellationToken ct)
    {
        var connection = await db.Set<RepositoryConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner, ct);
        var connected = connection is not null && connection.BaseUrl == Host;
        return new(options.Value.Enabled, connected, Host, connected ? connection!.Login : null,
            !options.Value.Enabled ? "管理員尚未啟用 Gitea 連線。" : connected ? "使用你的 Gitea 權限讀取。匯入是獨立快照，分享知識庫前請確認內容適合分享。" : "請建立 read:user、read:repository、read:issue 的 token。token 只由後端加密保存。");
    }
    public async Task<RepositoryStatusDto> ConnectAsync(Guid owner, string token, CancellationToken ct)
    {
        Enabled();
        if (token.Length is < 20 or > 512 || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) throw new ApiException(400, "invalid_gitea_token", "請輸入有效的 Gitea 個人存取權杖。");
        using var identity = await client.GetAsync(token, "api/v1/user", ct);
        var login = Text(identity.RootElement, "login", 100);
        if (login.Length == 0) throw new ApiException(502, "gitea_identity_invalid", "Gitea 未回傳帳號資訊。");
        await writes.Gate.WaitAsync(ct);
        try
        {
            var row = await db.Set<RepositoryConnection>().SingleOrDefaultAsync(x => x.OwnerId == owner, ct);
            if (row is null) { row = new() { OwnerId = owner }; db.Add(row); }
            row.BaseUrl = Host; row.Login = login; row.ProtectedToken = Protector(owner).Protect(token); row.ConnectedAt = DateTimeOffset.UtcNow;
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, ResourceId = owner, Action = "repository.connected", Result = "connected" });
            await db.SaveChangesAsync(ct); return await StatusAsync(owner, ct);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task DisconnectAsync(Guid owner, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await db.Set<RepositoryConnection>().Where(x => x.OwnerId == owner).ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, ResourceId = owner, Action = "repository.disconnected", Result = "deleted" }); await db.SaveChangesAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<RepositoryPageDto> ListAsync(Guid owner, int page, CancellationToken ct)
    {
        if (page is < 1 or > 1000) throw new ApiException(400, "invalid_page", "分頁不正確。");
        using var json = await client.GetAsync(await TokenAsync(owner, ct), $"api/v1/user/repos?page={page}&limit=20", ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array) throw new ApiException(502, "gitea_response_invalid", "Gitea 回應格式不正確。");
        var rows = json.RootElement.EnumerateArray().Take(20).Select(x => new RepositoryDto(Text(x, "full_name", 201), Text(x, "description", 500),
            x.TryGetProperty("private", out var p) && p.GetBoolean(), Text(x, "default_branch", 160), Link(Text(x, "full_name", 201)))).ToArray();
        return new(rows, page, rows.Length == 20);
    }
    public async Task<RepositoryTreeDto> TreeAsync(Guid owner, string repo, string? commit, string? path, CancellationToken ct)
    {
        var route = RepositoryRoute(repo); var token = await TokenAsync(owner, ct); var folder = FilePath(path ?? "", true);
        if (string.IsNullOrEmpty(commit))
        {
            using var metadata = await client.GetAsync(token, route, ct);
            var branch = Text(metadata.RootElement, "default_branch", 160);
            if (branch.Length == 0) throw new ApiException(409, "repository_empty", "此程式庫尚無提交版本。");
            using var latest = await client.GetAsync(token, route + "/branches/" + Uri.EscapeDataString(branch), ct);
            commit = Text(latest.RootElement.GetProperty("commit"), "id", 64);
        }
        Commit(commit);
        using var json = await client.GetAsync(token, route + "/contents" + (folder.Length == 0 ? "" : "/" + EncodePath(folder)) + "?ref=" + commit, ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array) throw new ApiException(400, "repository_not_directory", "請選擇資料夾。");
        var entries = json.RootElement.EnumerateArray().Take(500).Select(x => new RepositoryEntryDto(Text(x, "name", 180), Text(x, "path", 500), Text(x, "type", 16),
            x.TryGetProperty("size", out var size) ? size.GetInt64() : 0)).OrderBy(x => x.Kind == "dir" ? 0 : 1).ThenBy(x => x.Name).ToArray();
        return new(repo, commit, folder, entries);
    }
    public async Task<RepositoryFileDto> FileAsync(Guid owner, string repo, string commit, string path, CancellationToken ct)
    {
        var route = RepositoryRoute(repo); Commit(commit); var file = FilePath(path, false);
        using var json = await client.GetAsync(await TokenAsync(owner, ct), route + "/contents/" + EncodePath(file) + "?ref=" + commit, ct);
        var root = json.RootElement;
        if (Text(root, "type", 16) != "file" || Text(root, "encoding", 20) != "base64" || !root.TryGetProperty("size", out var size) || size.GetInt64() > options.Value.MaxFileBytes)
            throw new ApiException(413, "repository_file_limit", "僅支援上限內的文字檔案，不支援目錄、LFS 與二進位檔。");
        try
        {
            var bytes = Convert.FromBase64String(root.GetProperty("content").GetString() ?? "");
            if (bytes.Length > options.Value.MaxFileBytes) throw new ApiException(413, "repository_file_limit", "檔案超過上限。");
            var text = new UTF8Encoding(false, true).GetString(bytes);
            if (text.Contains('\0')) throw new DecoderFallbackException();
            return new(repo, commit, file, text.TrimStart('\uFEFF'), Link(repo) + "/src/commit/" + commit + "/" + EncodePath(file));
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException) { throw new ApiException(400, "repository_not_text", "此檔案不是可辨識的 UTF-8 文字檔。"); }
    }
    public async Task<IReadOnlyList<RepositoryIssueDto>> IssuesAsync(Guid owner, string repo, CancellationToken ct)
    {
        var route = RepositoryRoute(repo);
        using var json = await client.GetAsync(await TokenAsync(owner, ct), route + "/issues?state=open&type=issues&limit=30", ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array) throw new ApiException(502, "gitea_response_invalid", "Gitea 回應格式不正確。");
        return json.RootElement.EnumerateArray().Take(30).Select(x => new RepositoryIssueDto(x.GetProperty("number").GetInt32(), Text(x, "title", 200), Text(x, "body", 8000), Text(x, "state", 16), Link(repo) + "/issues/" + x.GetProperty("number").GetInt32())).ToArray();
    }
    public async Task<DocumentDto> ImportAsync(Guid owner, RepositoryImportRequest body, CancellationToken ct)
    {
        await access.RequireAsync(owner, body.CollectionId, "knowledge", ct, write: true);
        // Read at the pinned commit again: browser content is never trusted as a source snapshot.
        var file = await FileAsync(owner, body.Repository, body.Commit, body.Path, ct);
        await writes.Gate.WaitAsync(ct);
        try
        {
            var old = await db.Set<RepositoryImport>().AsNoTracking().Where(x => x.OwnerId == owner && x.CollectionId == body.CollectionId && x.Repository == file.Repository && x.Commit == file.Commit && x.Path == file.Path && x.BaseUrl == Host)
                .Join(db.Set<KnowledgeDocument>().Where(x => !x.IsDeleted), x => x.DocumentId, d => d.Id, (x, d) => x.DocumentId).FirstOrDefaultAsync(ct);
            if (old != Guid.Empty) return await documents.DetailAsync(owner, old, ct);
            var text = $"Gitea: {file.Repository}\nPath: {file.Path}\nCommit: {file.Commit}\nSource: {file.Url}\n\n{file.Text}";
            var bytes = Encoding.UTF8.GetBytes(text); using var stream = new MemoryStream(bytes);
            var upload = new FormFile(stream, 0, bytes.Length, "file", string.Concat((file.Repository.Replace('/', '_') + "_" + Path.GetFileName(file.Path)).Take(170)) + ".txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" };
            var attachment = await attachments.UploadAsync(owner, upload, ct);
            var document = await documents.AddAsync(owner, body.CollectionId, attachment.Id, ct);
            db.Add(new RepositoryImport { OwnerId = owner, CollectionId = body.CollectionId, DocumentId = document.Id, Repository = file.Repository, Commit = file.Commit, Path = file.Path, BaseUrl = Host });
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, ResourceId = document.Id, Action = "repository.imported", Result = "snapshot" }); await db.SaveChangesAsync(ct);
            return document;
        }
        finally { writes.Gate.Release(); }
    }
    private async Task<string> TokenAsync(Guid owner, CancellationToken ct)
    {
        Enabled(); var connection = await db.Set<RepositoryConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.BaseUrl == Host, ct)
            ?? throw new ApiException(409, "gitea_not_connected", "請先連線自己的 Gitea 帳號。");
        try { return Protector(owner).Unprotect(connection.ProtectedToken); }
        catch (CryptographicException) { throw new ApiException(409, "gitea_reconnect_required", "授權無法解密，請重新連線 Gitea。"); }
    }
    private void Enabled() { if (!options.Value.Enabled) throw new ApiException(503, "gitea_disabled", "管理員尚未啟用 Gitea。"); }
    private string Link(string repo) => Host + repo.Split('/').Select(Uri.EscapeDataString).Aggregate((a, b) => a + "/" + b);
    public static string RepositoryRoute(string repo)
    {
        var parts = repo.Split('/');
        if (parts.Length != 2 || parts.Any(p => p.Length is < 1 or > 100 || p is "." or ".." || p.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.')))
            throw new ApiException(400, "invalid_repository", "程式庫名稱不正確。");
        return "api/v1/repos/" + string.Join('/', parts.Select(Uri.EscapeDataString));
    }
    public static void Commit(string value) { if (value.Length is not (40 or 64) || !value.All(char.IsAsciiHexDigit)) throw new ApiException(400, "invalid_commit", "請選擇固定的 commit 版本。"); }
    public static string FilePath(string value, bool directory)
    {
        if (value.Length > 500 || (!directory && value.Length == 0) || value.Contains('\\') || value.Any(char.IsControl)
            || (value.Length > 0 && value.Split('/').Any(p => p.Length == 0 || p is "." or ".."))) throw new ApiException(400, "invalid_repository_path", "檔案路徑不正確。");
        return value;
    }
    private static string EncodePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
    private static string Text(JsonElement element, string key, int max) => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? string.Concat((value.GetString() ?? "").Take(max)) : "";
}
