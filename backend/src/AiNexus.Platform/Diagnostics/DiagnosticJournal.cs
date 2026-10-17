using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Diagnostics;

public interface IDiagnosticJournal
{
    Task AppendAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct);
    Task ReplayAsync(IDiagnosticStore store, CancellationToken ct);
    void Cleanup();
}

/// <summary>當本機診斷日誌磁碟容量超出上限時拋出之例外狀況。</summary>
public sealed class DiagnosticCapacityException() : IOException("Diagnostic capacity exhausted.");

/// <summary>
/// 診斷日誌核心：採僅附加（Append-only）分段儲存，依曆日與容量大小自動滾動（Rolling）。
/// 寫入本機磁碟並強制落盤（fsync）後，才非同步回放匯入至 SQL；SQL 認可後原子性更新位元組游標檢查點（Checkpoint）。
/// </summary>
public sealed class DiagnosticJournal(IOptions<DiagnosticOptions> options, DiagnosticHealth health, IHostEnvironment environment, TimeProvider? clock = null) : IDiagnosticJournal, IDisposable
{
    private const int MaximumRecordBytes = 65536;
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = 8 };
    private readonly string root = Resolve(options.Value.Directory, environment);
    private readonly string session = Guid.NewGuid().ToString("N");
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private FileStream? owner, writer;
    private string? currentFile;
    private DateOnly currentDate;
    private long sequence;
    private readonly object lifetime = new();
    private readonly HashSet<CapacityLease> leases = [];
    private bool disposed;

    public string Root => root;
    private string SessionDirectory => Path.Combine(root, session);

    public static string Resolve(string configured, IHostEnvironment environment)
    {
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData), "AiNexus", "diagnostics")
            : configured;

        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidOperationException("Diagnostics.Directory must be an absolute host-local external directory.");

        var full = Path.GetFullPath(path);
        var content = Path.GetFullPath(environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (full.StartsWith(content, StringComparison.OrdinalIgnoreCase) || string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), content.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Diagnostics.Directory must be outside the deployment directory.");

        for (var directory = new DirectoryInfo(full); directory is not null; directory = directory.Parent)
        {
            if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("Diagnostics.Directory must use a physical directory without reparse points.");
        }

        return full;
    }

    private void Initialize()
    {
        lock (lifetime)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (owner is not null) return;
            System.IO.Directory.CreateDirectory(SessionDirectory);
            owner = new FileStream(Path.Combine(SessionDirectory, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
    }

    private IEnumerable<string> Sessions() => System.IO.Directory.Exists(root)
        ? System.IO.Directory.EnumerateDirectories(root).Where(d => Guid.TryParseExact(Path.GetFileName(d), "N", out _))
        : [];

    // 短期的本機主機互斥鎖，協調所有在線的 IIS 執行處理程序共同遵守容量配額；SQL 存取期間絕不持有此鎖。
    private sealed class CapacityLease(DiagnosticJournal journal, FileStream file) : IDisposable, IAsyncDisposable
    {
        private FileStream? handle = file;

        public void Dispose()
        {
            Interlocked.Exchange(ref handle, null)?.Dispose();
            lock (journal.lifetime) journal.leases.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private CapacityLease TryCapacityLease()
    {
        lock (lifetime)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var lease = new CapacityLease(this, new FileStream(Path.Combine(root, "capacity.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            leases.Add(lease);
            return lease;
        }
    }

    private async Task<CapacityLease> CapacityLeaseAsync(CancellationToken ct)
    {
        var deadline = System.Environment.TickCount64 + 2000;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return TryCapacityLease();
            }
            catch (IOException) when (System.Environment.TickCount64 < deadline)
            {
                await Task.Delay(20, ct);
            }
        }
    }

    private void MeasureUsage()
    {
        long disk = 0, pending = 0;
        foreach (var directory in Sessions())
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(directory, "*.jsonl"))
            {
                var length = new FileInfo(file).Length;
                var offset = Cursor(file);
                disk += length;
                pending += offset > length ? length : length - offset;
            }
        }
        Interlocked.Exchange(ref health.DiskBytes, disk);
        Interlocked.Exchange(ref health.PendingBytes, pending);
    }

    private static byte[] Encode(DiagnosticEvent item)
    {
        DiagnosticRedactor.Normalize(item);
        var payload = JsonSerializer.SerializeToUtf8Bytes(item, Json);
        // 限制 UTF-8 位元組數：非 ASCII 字元與跳脫可能會使 bounded .NET 字串長度膨脹
        if (payload.Length > 60000)
        {
            item.ExceptionDetail = item.ExceptionDetail is null ? null : DiagnosticRedactor.Text(item.ExceptionDetail, 6000);
            item.PropertiesJson = "{}";
            payload = JsonSerializer.SerializeToUtf8Bytes(item, Json);
        }
        var prefix = Encoding.UTF8.GetBytes("{\"checksum\":\"" + Convert.ToHexString(SHA256.HashData(payload)) + "\",\"event\":");
        var result = new byte[prefix.Length + payload.Length + 2];
        prefix.CopyTo(result, 0);
        payload.CopyTo(result, prefix.Length);
        result[^2] = (byte)'}';
        result[^1] = (byte)'\n';
        if (result.Length > MaximumRecordBytes) throw new InvalidDataException("Diagnostic record exceeded its byte limit.");
        return result;
    }

    public async Task AppendAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;
        Initialize();
        var records = events.Select(Encode).ToArray();
        var bytes = records.Sum(x => (long)x.Length);
        await using var capacity = await CapacityLeaseAsync(ct);
        MeasureUsage();
        if (Interlocked.Read(ref health.DiskBytes) + bytes > options.Value.MaxDiskBytes) throw new DiagnosticCapacityException();
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var index = 0;
        while (index < records.Length)
        {
            if (writer is null || day != currentDate || writer.Length + records[index].Length > options.Value.FileSizeBytes)
            {
                if (writer is not null)
                {
                    await writer.DisposeAsync();
                    writer = null;
                }
                currentDate = day;
                currentFile = Path.Combine(SessionDirectory, $"{day:yyyyMMdd}-{++sequence:D8}.jsonl");
                writer = new FileStream(currentFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 65536, FileOptions.Asynchronous);
            }
            var position = writer.Position;
            var count = 0;
            try
            {
                do
                {
                    await writer.WriteAsync(records[index++], ct);
                    count++;
                }
                while (index < records.Length && writer.Position + records[index].Length <= options.Value.FileSizeBytes);

                await writer.FlushAsync(ct);
                writer.Flush(flushToDisk: true);
            }
            catch
            {
                try
                {
                    writer.SetLength(position);
                    writer.Position = position;
                }
                catch (IOException)
                {
                }
                throw;
            }
            Interlocked.Add(ref health.DiskBytes, writer.Position - position);
            Interlocked.Add(ref health.PendingBytes, writer.Position - position);
            Interlocked.Add(ref health.Written, count);
            health.LastFileWrite = clock.GetUtcNow();
        }
    }

    private static long Cursor(string path)
    {
        var file = path + ".cursor";
        return File.Exists(file) && long.TryParse(File.ReadAllText(file), CultureInfo.InvariantCulture, out var offset) && offset >= 0 ? offset : 0;
    }

    /// <summary>
    /// 將本機分段日誌重放回放至 SQL 儲存庫。
    /// 即使在離線啟動時，磁碟用量與待處理積壓仍保持可見。每個存活處理程序獨佔所屬工作目錄；離線殘留目錄由任一匯入器依序接管復原。
    /// </summary>
    public async Task ReplayAsync(IDiagnosticStore store, CancellationToken ct)
    {
        if (!System.IO.Directory.Exists(root)) return;

        // 離線啟動時磁碟積壓仍保持可見，不受 SQL 可用性影響
        await using (var capacity = await CapacityLeaseAsync(ct))
        {
            MeasureUsage();
        }

        // 每個在線處理程序排他性擁有自身目錄；離線殘留目錄由單一匯入器接管復原
        foreach (var directory in Sessions().OrderBy(d => d == SessionDirectory ? 0 : 1).ThenBy(d => d, StringComparer.Ordinal))
        {
            FileStream? recoveryLease = null;
            var own = string.Equals(directory, SessionDirectory, StringComparison.OrdinalIgnoreCase);
            if (!own)
            {
                try
                {
                    recoveryLease = new FileStream(Path.Combine(directory, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    continue;
                }
            }

            try
            {
                foreach (var file in System.IO.Directory.EnumerateFiles(directory, "*.jsonl").Order(StringComparer.Ordinal))
                {
                    ct.ThrowIfCancellationRequested();
                    var length = new FileInfo(file).Length;
                    var offset = Cursor(file);

                    if (offset > length)
                    {
                        offset = 0;
                        health.Emergency("journal_checkpoint_invalid");
                    }
                    if (offset == length) continue;

                    // 限制每個分段/週期的復原工作量，同時允許每個衝刷週期內執行多個批次
                    for (var batch = 0; batch < 20 && offset < length; batch++)
                    {
                        var result = await ReadAsync(file, offset, !own || file != currentFile, ct);
                        if (result.Events.Count > 0)
                        {
                            await store.WriteAsync(result.Events, ct);
                            Interlocked.Add(ref health.Replayed, result.Events.Count);
                            health.LastSqlWrite = clock.GetUtcNow();
                        }
                        if (result.Offset <= offset) break;

                        var temporary = file + ".cursor.tmp";
                        await File.WriteAllTextAsync(temporary, result.Offset.ToString(CultureInfo.InvariantCulture), ct);
                        File.Move(temporary, file + ".cursor", overwrite: true);
                        offset = result.Offset;
                    }
                }
            }
            finally
            {
                recoveryLease?.Dispose();
            }
        }

        await using (var capacity = await CapacityLeaseAsync(ct))
        {
            MeasureUsage();
        }
    }

    private async Task<(List<DiagnosticEvent> Events, long Offset)> ReadAsync(string path, long offset, bool sealedFile, CancellationToken ct)
    {
        var events = new List<DiagnosticEvent>();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.Asynchronous);
        stream.Position = offset;
        var buffer = new byte[65536];
        var line = new List<byte>(2048);
        var oversized = false;
        var processed = 0;
        var consumed = offset;
        var checkpoint = offset;

        while (processed < options.Value.BatchSize)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;

            for (var index = 0; index < read; index++)
            {
                consumed++;
                if (buffer[index] != (byte)'\n')
                {
                    if (line.Count < MaximumRecordBytes) line.Add(buffer[index]);
                    else oversized = true;
                    continue;
                }

                processed++;
                try
                {
                    if (oversized) throw new JsonException();
                    using var document = JsonDocument.Parse(line.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
                    var raw = document.RootElement.GetProperty("event").GetRawText();
                    if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))) != document.RootElement.GetProperty("checksum").GetString())
                        throw new JsonException();

                    var item = JsonSerializer.Deserialize<DiagnosticEvent>(raw, Json) ?? throw new JsonException();
                    // 復原時重新套用邊界與脫敏規則：本機檔案亦不可作為任意未受信任中繼資料的注入管道
                    if (raw.Length > 48000 || item.LogId == Guid.Empty || item.Level is < LogLevel.Trace or > LogLevel.Critical)
                        throw new JsonException();

                    DiagnosticRedactor.Normalize(item);
                    events.Add(item);
                }
                catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
                {
                    Interlocked.Increment(ref health.Corrupt);
                    health.Emergency("journal_corrupt_record", 1);
                }

                line.Clear();
                oversized = false;
                checkpoint = consumed;

                if (processed >= options.Value.BatchSize) return (events, checkpoint);
            }
        }

        if (sealedFile && (line.Count > 0 || oversized))
        {
            Interlocked.Increment(ref health.Corrupt);
            health.Emergency("journal_truncated_record", 1);
            checkpoint = consumed;
        }

        return (events, checkpoint);
    }

    /// <summary>清理已完全認可同步至 SQL 且超出保留期限的歷史日誌段。未經確認同步的日誌絕不因容量或保留限制而刪除。</summary>
    public void Cleanup()
    {
        if (!System.IO.Directory.Exists(root)) return;
        CapacityLease capacity;
        try
        {
            capacity = TryCapacityLease();
        }
        catch (IOException)
        {
            return;
        }

        using (capacity)
        {
            var remaining = options.Value.CleanupBatchSize;
            foreach (var directory in Sessions())
            {
                if (remaining == 0) break;
                FileStream? lease = null;
                if (directory != SessionDirectory)
                {
                    try
                    {
                        lease = new FileStream(Path.Combine(directory, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    }
                    catch (IOException)
                    {
                        continue;
                    }
                }

                try
                {
                    foreach (var file in System.IO.Directory.EnumerateFiles(directory, "*.jsonl"))
                    {
                        if (remaining == 0) break;
                        if (file == currentFile) continue;
                        var info = new FileInfo(file);
                        // 未經確認同步至 SQL 的資料絕不予以刪除，哪怕已達保留天數上限
                        if (info.LastWriteTimeUtc < clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.FileRetentionDays) && Cursor(file) == info.Length)
                        {
                            File.Delete(file);
                            File.Delete(file + ".cursor");
                            File.Delete(file + ".cursor.tmp");
                            remaining--;
                        }
                    }
                }
                finally
                {
                    lease?.Dispose();
                }

                if (directory != SessionDirectory && !System.IO.Directory.EnumerateFiles(directory, "*.jsonl").Any())
                {
                    // 絕不進行遞迴刪除：僅在受管會話目錄為空時移除已知鎖定檔與目錄自身
                    try
                    {
                        File.Delete(Path.Combine(directory, "owner.lock"));
                        System.IO.Directory.Delete(directory, recursive: false);
                    }
                    catch (IOException)
                    {
                        // 可能有其他匯入器競爭，或維運人員遺留的除錯檔案
                    }
                }
            }
            MeasureUsage();
        }
    }

    public void Dispose()
    {
        CapacityLease[] active;
        lock (lifetime)
        {
            if (disposed) return;
            disposed = true;
            active = leases.ToArray();
        }

        // 處置邏輯亦涵蓋被強制逾時終止的主機：日誌生命週期結束後，絕不允許任何匯入器繼續保留容量控制代碼
        try
        {
            writer?.Dispose();
        }
        finally
        {
            owner?.Dispose();
            foreach (var lease in active) lease.Dispose();
        }
    }
}
