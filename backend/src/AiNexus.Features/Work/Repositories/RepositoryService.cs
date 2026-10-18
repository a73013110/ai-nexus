using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Work.Repositories.Browsing;
using AiNexus.Features.Work.Repositories.Connections;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Repositories;

/// <summary>
/// 通往 Gitea 的唯一路徑：解密使用者自己的 token、組出受控路徑，並提供多個切片共用的讀取方法。
/// 預期內的失敗（未連線、名稱不合法、內容不支援）以結果回傳；Gitea 的傳輸失敗與拒絕則由 <see cref="IGiteaClient"/> 丟出 <see cref="ExternalServiceException"/>。
/// </summary>
public sealed class RepositoryService(AppDbContext db, IGiteaClient client, IDataProtectionProvider protection, IOptions<GiteaOptions> options)
{
    // 同一個實例（即同一個請求）只解密一次 token；解密失敗的結果也會被快取，不會在同一請求中重試。
    private Result<string>? token;

    public string BaseUrl { get; } = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/").AbsoluteUri;
    public bool Enabled => options.Value.Enabled;
    public GiteaOptions Options => options.Value;

    public async Task<RepositoryStatusDto> StatusAsync(Guid owner, CancellationToken ct)
    {
        var connection = await db.Set<RepositoryConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner, ct);
        var connected = connection is not null && connection.BaseUrl == BaseUrl;
        return new(Enabled, connected, BaseUrl, connected ? connection!.Login : null, connected ? connection!.ConnectedAt : null);
    }

    /// <summary>為此使用者與目前主機加密 token；主機變更後，舊的密文就無法解開。</summary>
    public string Protect(Guid owner, string token) => Protector(owner).Protect(token);

    public async Task<Result<JsonDocument>> GetAsync(Guid owner, string path, CancellationToken ct)
    {
        var access = await TokenAsync(owner, ct);
        return access.IsSuccess ? Result<JsonDocument>.Ok(await client.GetAsync(access.Value!, path, ct)) : access.Error!;
    }

    /// <summary>要求 JSON 陣列回應；其他形狀代表 Gitea 回應格式不正確，直接拋出例外。</summary>
    public async Task<Result<JsonDocument>> GetListAsync(Guid owner, string path, CancellationToken ct)
    {
        var read = await GetAsync(owner, path, ct);
        if (read.IsSuccess && read.Value!.RootElement.ValueKind != JsonValueKind.Array)
        {
            read.Value.Dispose();
            throw new ExternalServiceException(RepositoriesErrors.ResponseInvalid);
        }
        return read;
    }

    public async Task<Result<JsonDocument>> PostCommentAsync(Guid owner, string repo, int number, string body, CancellationToken ct)
    {
        var access = await TokenAsync(owner, ct);
        return access.IsSuccess ? Result<JsonDocument>.Ok(await client.PostCommentAsync(access.Value!, Route(repo) + "/issues/" + number + "/comments", body, ct)) : access.Error!;
    }

    public async Task<Result> RequireCommitAsync(Guid owner, string repo, string commit, CancellationToken ct)
    {
        if (!IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (!IsCommit(commit)) return RepositoriesErrors.InvalidCommit;
        var read = await GetAsync(owner, Route(repo) + "/git/commits/" + commit + "?stat=false&files=false&verification=false", ct);
        if (!read.IsSuccess) return read.Error!;
        read.Value!.Dispose();
        return Result.Success;
    }

    /// <summary>單一 commit 與其父 commit 的差異，或兩個 commit 之間的淨變更。</summary>
    public async Task<Result<string>> DiffAsync(Guid owner, string repo, string head, string? basis, CancellationToken ct)
    {
        if (!IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (!IsCommit(head) || (basis is not null && !IsCommit(basis))) return RepositoriesErrors.InvalidCommit;
        var access = await TokenAsync(owner, ct);
        if (!access.IsSuccess) return access.Error!;
        var route = Route(repo);
        var path = basis is null ? route + "/git/commits/" + head + ".diff" : route + "/compare/" + basis + ".." + head + "?output=diff";
        var diff = await client.GetTextAsync(access.Value!, path, MaxDiffBytes, ct);
        if (diff is null) return RepositoriesErrors.DiffLimit;
        if (!string.IsNullOrWhiteSpace(diff) && !diff.TrimStart().StartsWith("diff --git ", StringComparison.Ordinal)) return RepositoriesErrors.DiffUnsupported;
        return diff;
    }

    public const int MaxDiffBytes = 256000;

    /// <summary>
    /// 在指定的 commit 讀取文字檔。交給對話或知識庫時會重新讀取一次，不沿用瀏覽器送回的內容，
    /// 確保匯入的確實是該 commit 的檔案。
    /// </summary>
    public async Task<Result<RepositoryFileDto>> FileAsync(Guid owner, string repo, string commit, string path, CancellationToken ct)
    {
        if (!IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (!IsCommit(commit)) return RepositoriesErrors.InvalidCommit;
        if (!IsFilePath(path, false)) return RepositoriesErrors.InvalidPath;
        var read = await GetAsync(owner, Route(repo) + "/contents/" + EncodePath(path) + "?ref=" + commit, ct);
        if (!read.IsSuccess) return read.Error!;
        using var json = read.Value!;
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object || GiteaJson.Text(root, "type", 16) != "file") return RepositoriesErrors.NotText;
        var size = GiteaJson.Number(root, "size");
        if (size > options.Value.MaxFileBytes || GiteaJson.Text(root, "encoding", 20) != "base64") return RepositoriesErrors.FileLimit;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(GiteaJson.Text(root, "content", options.Value.MaxFileBytes * 2)); }
        catch (FormatException) { return RepositoriesErrors.NotText; }
        if (bytes.Length > options.Value.MaxFileBytes) return RepositoriesErrors.FileLimit;
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { return RepositoriesErrors.NotText; }
        if (text.Contains('\0')) return RepositoriesErrors.NotText;
        return new RepositoryFileDto(commit, path, text.TrimStart('﻿'), bytes.Length, Link(repo) + "/src/commit/" + commit + "/" + EncodePath(path));
    }

    private async Task<Result<string>> TokenAsync(Guid owner, CancellationToken ct)
    {
        if (token is { } cached) return cached;
        if (!Enabled) return RepositoriesErrors.Disabled;
        var connection = await db.Set<RepositoryConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.BaseUrl == BaseUrl, ct);
        if (connection is null) return RepositoriesErrors.NotConnected;
        try { token = Protector(owner).Unprotect(connection.ProtectedToken); }
        catch (CryptographicException) { token = RepositoriesErrors.ReconnectRequired; }
        return token.Value;
    }

    private IDataProtector Protector(Guid owner) => protection.CreateProtector("AiNexus.Gitea.UserToken.v1", owner.ToString("N"), BaseUrl);

    /// <summary>程式庫在 Gitea 主機上的頁面網址。</summary>
    public string Link(string repo) => BaseUrl + string.Join('/', repo.Split('/').Select(Uri.EscapeDataString));

    public static string Route(string repo) => "api/v1/repos/" + string.Join('/', repo.Split('/').Select(Uri.EscapeDataString));
    public static string EncodePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    public static bool IsRepository([NotNullWhen(true)] string? repo) => repo?.Split('/') is { Length: 2 } parts &&
        parts.All(p => p.Length is >= 1 and <= 100 && p is not ("." or "..") && p.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'));

    public static bool IsCommit([NotNullWhen(true)] string? value) => value is { Length: 40 or 64 } && value.All(char.IsAsciiHexDigit);

    /// <summary>分支、標籤或 commit 名稱；只會以跳脫後的查詢參數送到 Gitea。</summary>
    public static bool IsRef([NotNullWhen(true)] string? value) => value is { Length: >= 1 and <= 250 } && !value.Any(char.IsControl) &&
        !value.Contains("..", StringComparison.Ordinal) && !value.Any(c => c is ' ' or '~' or '^' or ':' or '?' or '*' or '[' or '\\');

    public static bool IsFilePath([NotNullWhen(true)] string? value, bool directory) => value is not null && value.Length <= 500 && (directory || value.Length > 0) &&
        !value.Contains('\\') && !value.Any(char.IsControl) && (value.Length == 0 || value.Split('/').All(p => p.Length > 0 && p is not ("." or "..")));

    public static bool IsState(string? value) => value is "open" or "closed" or "all";
}
