using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Repositories;

/// <summary>
/// The read-only path to Gitea with the user's own token, which only this class encrypts and decrypts. Expected failures
/// (not connected, invalid names, unsupported content) are results; Gitea transport failures and malformed responses
/// stay exceptions, raised by <see cref="IGiteaClient"/> or here.
/// </summary>
public sealed class RepositoryService(NexusDbContext db, IGiteaClient client, IDataProtectionProvider protection, IOptions<GiteaOptions> options)
{
    private string Host => new Uri(options.Value.BaseUrl.TrimEnd('/') + "/").AbsoluteUri;
    public string BaseUrl => Host;
    public bool Enabled => options.Value.Enabled;

    public async Task<RepositoryStatusDto> StatusAsync(Guid owner, CancellationToken ct)
    {
        var connection = await db.Set<RepositoryConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner, ct);
        var connected = connection is not null && connection.BaseUrl == Host;
        return new(options.Value.Enabled, connected, Host, connected ? connection!.Login : null,
            !options.Value.Enabled ? "管理員尚未啟用 Gitea 連線。" : connected ? "使用你的 Gitea 權限讀取。匯入是獨立快照，分享知識庫前請確認內容適合分享。" : "請建立 read:user、read:repository、read:issue 的 token。token 只由後端加密保存。");
    }

    /// <summary>Encrypts a token for this user and the current host; a host change makes it unreadable.</summary>
    public string Protect(Guid owner, string token) => Protector(owner).Protect(token);

    public async Task<Result> RequireCommitAsync(Guid owner, string repo, string commit, CancellationToken ct)
    {
        if (!IsCommit(commit)) return RepositoriesErrors.InvalidCommit;
        var token = await TokenAsync(owner, ct);
        if (!token.IsSuccess) return token.Error;
        if (!IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        using var metadata = await client.GetAsync(token.Value, Route(repo) + "/git/commits/" + commit, ct);
        return Result.Success;
    }

    public async Task<Result<string>> DiffAsync(Guid owner, string repo, string head, string? basis, CancellationToken ct)
    {
        if (!IsCommit(head) || (basis is not null && !IsCommit(basis))) return RepositoriesErrors.InvalidCommit;
        if (!IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        var route = Route(repo);
        var path = basis is null ? route + "/git/commits/" + head + ".diff" : route + "/compare/" + basis + ".." + head + "?output=diff";
        var token = await TokenAsync(owner, ct);
        if (!token.IsSuccess) return token.Error;
        var diff = await client.GetTextAsync(token.Value, path, ct);
        if (!string.IsNullOrWhiteSpace(diff) && !diff.TrimStart().StartsWith("diff --git ", StringComparison.Ordinal)) return RepositoriesErrors.DiffUnsupported;
        return diff;
    }

    public async Task<Result<IReadOnlyList<RepositoryCommitDto>>> CommitsAsync(Guid owner, string repo, CancellationToken ct)
    {
        var token = await TokenAsync(owner, ct);
        if (!token.IsSuccess) return token.Error;
        if (!IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        using var json = await client.GetAsync(token.Value, Route(repo) + "/commits?limit=30", ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array) throw ResponseInvalid();
        return Result<IReadOnlyList<RepositoryCommitDto>>.Ok(json.RootElement.EnumerateArray().Take(30)
            .Select(x => new RepositoryCommitDto(Text(x, "sha", 64), Text(x.GetProperty("commit"), "message", 500))).ToArray());
    }

    public async Task<Result<RepositoryPageDto>> ListAsync(Guid owner, int page, CancellationToken ct)
    {
        if (page is < 1 or > 1000) return RepositoriesErrors.InvalidPage;
        var token = await TokenAsync(owner, ct);
        if (!token.IsSuccess) return token.Error;
        using var json = await client.GetAsync(token.Value, $"api/v1/user/repos?page={page}&limit=20", ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array) throw ResponseInvalid();
        var rows = json.RootElement.EnumerateArray().Take(20).Select(x => new RepositoryDto(Text(x, "full_name", 201), Text(x, "description", 500),
            x.TryGetProperty("private", out var p) && p.GetBoolean(), Text(x, "default_branch", 160), Link(Text(x, "full_name", 201)))).ToArray();
        return new RepositoryPageDto(rows, page, rows.Length == 20);
    }

    public async Task<Result<RepositoryTreeDto>> TreeAsync(Guid owner, string repo, string? commit, string? path, CancellationToken ct)
    {
        if (!IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        var route = Route(repo);
        var token = await TokenAsync(owner, ct);
        if (!token.IsSuccess) return token.Error;
        var folder = path ?? "";
        if (!IsFilePath(folder, true)) return RepositoriesErrors.InvalidPath;
        if (string.IsNullOrEmpty(commit))
        {
            using var metadata = await client.GetAsync(token.Value, route, ct);
            var branch = Text(metadata.RootElement, "default_branch", 160);
            if (branch.Length == 0) return RepositoriesErrors.Empty;
            using var latest = await client.GetAsync(token.Value, route + "/branches/" + Uri.EscapeDataString(branch), ct);
            commit = Text(latest.RootElement.GetProperty("commit"), "id", 64);
        }
        if (!IsCommit(commit)) return RepositoriesErrors.InvalidCommit;
        using var json = await client.GetAsync(token.Value, route + "/contents" + (folder.Length == 0 ? "" : "/" + EncodePath(folder)) + "?ref=" + commit, ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array) return RepositoriesErrors.NotDirectory;
        var entries = json.RootElement.EnumerateArray().Take(500).Select(x => new RepositoryEntryDto(Text(x, "name", 180), Text(x, "path", 500), Text(x, "type", 16),
            x.TryGetProperty("size", out var size) ? size.GetInt64() : 0)).OrderBy(x => x.Kind == "dir" ? 0 : 1).ThenBy(x => x.Name).ToArray();
        return new RepositoryTreeDto(repo, commit, folder, entries);
    }

    /// <summary>Reads a text file at a pinned commit. Imports call this again: browser content is never a trusted snapshot.</summary>
    public async Task<Result<RepositoryFileDto>> FileAsync(Guid owner, string repo, string commit, string path, CancellationToken ct)
    {
        if (!IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (!IsCommit(commit)) return RepositoriesErrors.InvalidCommit;
        if (!IsFilePath(path, false)) return RepositoriesErrors.InvalidPath;
        var token = await TokenAsync(owner, ct);
        if (!token.IsSuccess) return token.Error;
        using var json = await client.GetAsync(token.Value, Route(repo) + "/contents/" + EncodePath(path) + "?ref=" + commit, ct);
        var root = json.RootElement;
        if (Text(root, "type", 16) != "file" || Text(root, "encoding", 20) != "base64" || !root.TryGetProperty("size", out var size) || size.GetInt64() > options.Value.MaxFileBytes)
            return RepositoriesErrors.FileLimit;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(root.GetProperty("content").GetString() ?? ""); }
        catch (FormatException) { return RepositoriesErrors.NotText; }
        if (bytes.Length > options.Value.MaxFileBytes) return RepositoriesErrors.FileLimit;
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { return RepositoriesErrors.NotText; }
        if (text.Contains('\0')) return RepositoriesErrors.NotText;
        return new RepositoryFileDto(repo, commit, path, text.TrimStart('﻿'), Link(repo) + "/src/commit/" + commit + "/" + EncodePath(path));
    }

    public async Task<Result<IReadOnlyList<RepositoryIssueDto>>> IssuesAsync(Guid owner, string repo, CancellationToken ct)
    {
        if (!IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        var token = await TokenAsync(owner, ct);
        if (!token.IsSuccess) return token.Error;
        using var json = await client.GetAsync(token.Value, Route(repo) + "/issues?state=open&type=issues&limit=30", ct);
        if (json.RootElement.ValueKind != JsonValueKind.Array) throw ResponseInvalid();
        return Result<IReadOnlyList<RepositoryIssueDto>>.Ok(json.RootElement.EnumerateArray().Take(30).Select(x => new RepositoryIssueDto(x.GetProperty("number").GetInt32(),
            Text(x, "title", 200), Text(x, "body", 8000), Text(x, "state", 16), Link(repo) + "/issues/" + x.GetProperty("number").GetInt32())).ToArray());
    }

    private async Task<Result<string>> TokenAsync(Guid owner, CancellationToken ct)
    {
        if (!options.Value.Enabled) return RepositoriesErrors.Disabled;
        var connection = await db.Set<RepositoryConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.BaseUrl == Host, ct);
        if (connection is null) return RepositoriesErrors.NotConnected;
        try { return Protector(owner).Unprotect(connection.ProtectedToken); }
        catch (CryptographicException) { return RepositoriesErrors.ReconnectRequired; }
    }

    private IDataProtector Protector(Guid owner) => protection.CreateProtector("AiNexus.Gitea.UserToken.v1", owner.ToString("N"), Host);
    private string Link(string repo) => Host + repo.Split('/').Select(Uri.EscapeDataString).Aggregate((a, b) => a + "/" + b);
    private static ApiException ResponseInvalid() => new(502, "gitea_response_invalid", "Gitea 回應格式不正確。");

    public static bool IsRepository([NotNullWhen(true)] string? repo) => repo?.Split('/') is { Length: 2 } parts &&
        parts.All(p => p.Length is >= 1 and <= 100 && p is not ("." or "..") && p.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'));

    public static bool IsCommit([NotNullWhen(true)] string? value) => value is { Length: 40 or 64 } && value.All(char.IsAsciiHexDigit);

    public static bool IsFilePath([NotNullWhen(true)] string? value, bool directory) => value is not null && value.Length <= 500 && (directory || value.Length > 0) &&
        !value.Contains('\\') && !value.Any(char.IsControl) && (value.Length == 0 || value.Split('/').All(p => p.Length > 0 && p is not ("." or "..")));

    /// <summary>Throwing form of <see cref="IsRepository"/>: the controlled Gitea API path for a repository.</summary>
    public static string RepositoryRoute(string repo) => IsRepository(repo) ? Route(repo) : throw RepositoriesErrors.InvalidRepository.ToException();
    public static void Commit(string value) { if (!IsCommit(value)) throw RepositoriesErrors.InvalidCommit.ToException(); }
    public static string FilePath(string value, bool directory) => IsFilePath(value, directory) ? value : throw RepositoriesErrors.InvalidPath.ToException();

    private static string Route(string repo) => "api/v1/repos/" + string.Join('/', repo.Split('/').Select(Uri.EscapeDataString));
    private static string EncodePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
    internal static string Text(JsonElement element, string key, int max) => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? string.Concat((value.GetString() ?? "").Take(max)) : "";
}
