using AiNexus.BuildingBlocks;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace AiNexus.Modules.Attachments;

/// <summary>Keys are opaque immutable identifiers. Only domain services grant access to stored bytes.</summary>
public interface IAttachmentStorage
{
    Task WriteAsync(string key, ReadOnlyMemory<byte> bytes, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    IAsyncEnumerable<string> StaleKeysAsync(DateTimeOffset before, CancellationToken ct);
    Task VerifyAsync(CancellationToken ct);
}

public sealed class FileAttachmentStorage : IAttachmentStorage
{
    private readonly string root;
    public FileAttachmentStorage(IOptions<AttachmentOptions> options, IHostEnvironment environment)
    {
        root = ValidateRoot(options.Value.StoragePath, environment.ContentRootPath);
    }

    public static string ValidateRoot(string path, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("Attachments:StoragePath must be an absolute path outside the website directory.");
        var resolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var website = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentRoot));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (resolved.Equals(website, comparison) || resolved.StartsWith(website + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("Attachments:StoragePath must be outside the website directory.");
        return resolved;
    }

    private string PathFor(string key)
    {
        if (!ValidKey(key))
            throw new InvalidDataException("Invalid attachment storage key.");
        return Path.Combine(root, key[..2], key.Substring(2, 2), key + ".blob");
    }

    private static bool ValidKey(string key) => key.Length == 32 && key.All(c => char.IsAsciiHexDigit(c) && !char.IsAsciiLetterUpper(c));

    public async IAsyncEnumerable<string> StaleKeysAsync(DateTimeOffset before, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!Directory.Exists(root)) yield break;
        var entries = Directory.EnumerateFiles(root, "*.blob*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false });
        var count = 0;
        foreach (var path in entries)
        {
            ct.ThrowIfCancellationRequested();
            // Yield regularly even when every entry is recent, keeping large scans cancellable.
            if (++count % 100 == 0) await Task.Yield();
            var name = Path.GetFileName(path);
            var suffix = name.EndsWith(".blob.upload", StringComparison.Ordinal) ? ".blob.upload" : name.EndsWith(".blob", StringComparison.Ordinal) ? ".blob" : null;
            if (suffix is null) continue;
            var key = name[..^suffix.Length];
            if (!ValidKey(key) || !path.Equals(PathFor(key) + (suffix == ".blob.upload" ? ".upload" : ""), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) continue;
            if (File.GetLastWriteTimeUtc(path) < before.UtcDateTime) yield return key;
        }
    }

    public async Task WriteAsync(string key, ReadOnlyMemory<byte> bytes, CancellationToken ct)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var stream = new FileStream(path + ".upload", FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes, ct);
            await stream.FlushAsync(ct);
        }
        ct.ThrowIfCancellationRequested();
        File.Move(path + ".upload", path, overwrite: false);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try { return Task.FromResult<Stream>(new FileStream(PathFor(key), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan)); }
        catch (FileNotFoundException) { throw new ApiException(503, "attachment_content_missing", "附件原檔暫時無法讀取，請聯絡管理員檢查儲存與備份。"); }
        catch (DirectoryNotFoundException) { throw new ApiException(503, "attachment_content_missing", "附件原檔暫時無法讀取，請聯絡管理員檢查儲存與備份。"); }
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = PathFor(key);
        DeleteFile(path);
        DeleteFile(path + ".upload");
        return Task.CompletedTask;
    }

    private static void DeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (DirectoryNotFoundException) { } // Missing shard directories are already deleted.
    }

    public async Task VerifyAsync(CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N");
        try { await WriteAsync(key, new byte[] { 42 }, ct); await using var read = await OpenReadAsync(key, ct); }
        finally { await DeleteAsync(key, CancellationToken.None); }
    }
}
