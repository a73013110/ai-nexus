using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Persistence;
using AiNexus.Features.Repositories.Browsing;
using AiNexus.Features.Repositories.Connections;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Repositories;

/// <summary>
/// The only path to Gitea: it decrypts the user's own token (once per request), builds controlled paths and owns the reads
/// several slices share. Expected failures (not connected, invalid names, unsupported content) are results; Gitea transport
/// failures and refusals stay <see cref="ExternalServiceException"/> raised by <see cref="IGiteaClient"/>.
/// </summary>
public sealed class RepositoryService(NexusDbContext db, IGiteaClient client, IDataProtectionProvider protection, IOptions<GiteaOptions> options)
{
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

    /// <summary>Encrypts a token for this user and the current host; a host change makes it unreadable.</summary>
    public string Protect(Guid owner, string token) => Protector(owner).Protect(token);

    public async Task<Result<JsonDocument>> GetAsync(Guid owner, string path, CancellationToken ct)
    {
        var access = await TokenAsync(owner, ct);
        return access.IsSuccess ? Result<JsonDocument>.Ok(await client.GetAsync(access.Value!, path, ct)) : access.Error!;
    }

    /// <summary>A JSON array response; anything else is a malformed Gitea response.</summary>
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

    /// <summary>A commit against its parent, or the net change between two commits.</summary>
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

    /// <summary>Reads a text file at a pinned commit. Handoffs call this again: browser content is never a trusted snapshot.</summary>
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

    /// <summary>The repository's page on the Gitea host.</summary>
    public string Link(string repo) => BaseUrl + string.Join('/', repo.Split('/').Select(Uri.EscapeDataString));

    public static string Route(string repo) => "api/v1/repos/" + string.Join('/', repo.Split('/').Select(Uri.EscapeDataString));
    public static string EncodePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    public static bool IsRepository([NotNullWhen(true)] string? repo) => repo?.Split('/') is { Length: 2 } parts &&
        parts.All(p => p.Length is >= 1 and <= 100 && p is not ("." or "..") && p.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'));

    public static bool IsCommit([NotNullWhen(true)] string? value) => value is { Length: 40 or 64 } && value.All(char.IsAsciiHexDigit);

    /// <summary>A branch, tag or commit name, passed to Gitea only as an escaped query value.</summary>
    public static bool IsRef([NotNullWhen(true)] string? value) => value is { Length: >= 1 and <= 250 } && !value.Any(char.IsControl) &&
        !value.Contains("..", StringComparison.Ordinal) && !value.Any(c => c is ' ' or '~' or '^' or ':' or '?' or '*' or '[' or '\\');

    public static bool IsFilePath([NotNullWhen(true)] string? value, bool directory) => value is not null && value.Length <= 500 && (directory || value.Length > 0) &&
        !value.Contains('\\') && !value.Any(char.IsControl) && (value.Length == 0 || value.Split('/').All(p => p.Length > 0 && p is not ("." or "..")));

    public static bool IsState(string? value) => value is "open" or "closed" or "all";
}
