using System.Data;
using System.Reflection;
using System.Transactions;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Modules.Operations;

namespace AiNexus.BuildingBlocks.Diagnostics;

public static class DiagnosticSuppression
{
    private static readonly AsyncLocal<int> Depth = new();
    public static bool Active => Depth.Value > 0;
    public static IDisposable Enter() { Depth.Value++; return new Exit(); }
    private sealed class Exit : IDisposable { public void Dispose() => Depth.Value--; }
}

public interface IDiagnosticStore
{
    Task WriteAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct);
    Task CleanupAsync(CancellationToken ct);
}

/// <summary>New connection/transaction, never the caller's business DbContext or transaction.</summary>
public sealed class DiagnosticStore(IServiceScopeFactory scopes, IOptions<DiagnosticOptions> options, DiagnosticHealth health) : IDiagnosticStore
{
    private static readonly PropertyInfo[] Columns = typeof(DiagnosticEvent).GetProperties();
    private bool configurationRecorded;
    public async Task WriteAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;
        using var suppress = DiagnosticSuppression.Enter();
        using var ambient = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        if (!db.Database.IsSqlServer())
        {
            var ids = events.Select(x => x.LogId).Distinct().ToArray();
            var existing = await db.Set<DiagnosticEvent>().Where(x => ids.Contains(x.LogId)).Select(x => x.LogId).ToArrayAsync(ct);
            var count = await db.Set<DiagnosticEvent>().LongCountAsync(ct); Interlocked.Exchange(ref health.EstimatedSqlRows, count);
            if (ids.Length > existing.Length && count + ids.Length - existing.Length > options.Value.MaxSqlRows) throw new DiagnosticCapacityException();
            db.AddRange(events.DistinctBy(x => x.LogId).Where(x => !existing.Contains(x.LogId)));
            await db.SaveChangesAsync(ct); return;
        }
        var settings = options.Value;
        var connectionString = new SqlConnectionStringBuilder(db.Database.GetConnectionString()) { Enlist = false, ConnectTimeout = settings.SqlTimeoutSeconds }.ConnectionString;
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(ct);
        // Metadata row count avoids COUNT(*) on a growing table. Capacity is an approximate soft limit
        // (concurrent import batches and SQL metadata estimates can overshoot); no early retention purge.
        await using (var count = new SqlCommand("SELECT COALESCE(SUM([rows]),0) FROM sys.partitions WHERE [object_id]=OBJECT_ID(N'operations.DiagnosticEvents') AND [index_id] IN (0,1);", connection) { CommandTimeout = settings.SqlTimeoutSeconds })
        {
            var rows = Convert.ToInt64(await count.ExecuteScalarAsync(ct)); Interlocked.Exchange(ref health.EstimatedSqlRows, rows);
            if (rows + events.Count > settings.MaxSqlRows)
            {
                var ids = events.Select(x => x.LogId).Distinct().ToArray();
                await using var exists = new SqlCommand("SELECT COUNT(*) FROM [operations].[DiagnosticEvents] WHERE [LogId] IN (" + string.Join(',', ids.Select((_, i) => "@id" + i)) + ");", connection) { CommandTimeout = settings.SqlTimeoutSeconds };
                for (var i = 0; i < ids.Length; i++) exists.Parameters.AddWithValue("@id" + i, ids[i]);
                var existing = Convert.ToInt64(await exists.ExecuteScalarAsync(ct));
                if (ids.Length > existing && rows + ids.Length - existing > settings.MaxSqlRows) throw new DiagnosticCapacityException();
            }
        }
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var columns = string.Join(",", Columns.Select(x => "[" + x.Name + "]"));
        await using (var create = new SqlCommand("SELECT TOP (0) " + columns + " INTO #LogBatch FROM [operations].[DiagnosticEvents];", connection, transaction) { CommandTimeout = settings.SqlTimeoutSeconds })
            await create.ExecuteNonQueryAsync(ct);
        var table = new DataTable();
        foreach (var column in Columns) table.Columns.Add(column.Name, column.PropertyType == typeof(LogLevel) ? typeof(int) : Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType);
        foreach (var item in events.DistinctBy(x => x.LogId)) table.Rows.Add(Columns.Select(x => x.GetValue(item) is { } value ? value is LogLevel l ? (object)(int)l : value : DBNull.Value).ToArray());
        using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction) { DestinationTableName = "#LogBatch", BatchSize = settings.BatchSize, BulkCopyTimeout = settings.SqlTimeoutSeconds })
        {
            foreach (var column in Columns) bulk.ColumnMappings.Add(column.Name, column.Name);
            await bulk.WriteToServerAsync(table, ct);
        }
        await using (var insert = new SqlCommand("INSERT INTO [operations].[DiagnosticEvents] (" + columns + ") SELECT " + string.Join(",", Columns.Select(x => "b.[" + x.Name + "]")) +
            " FROM #LogBatch b WHERE NOT EXISTS (SELECT 1 FROM [operations].[DiagnosticEvents] d WITH (UPDLOCK,HOLDLOCK) WHERE d.[LogId]=b.[LogId]);", connection, transaction) { CommandTimeout = settings.SqlTimeoutSeconds })
            await insert.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task CleanupAsync(CancellationToken ct)
    {
        using var suppress = DiagnosticSuppression.Enter();
        using var ambient = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        db.Database.SetCommandTimeout(options.Value.SqlTimeoutSeconds);
        if (!configurationRecorded)
        {
            var o = options.Value;
            // Audit policy changes once per configuration, after SQL becomes available. Locations and
            // exporter URLs are deliberately excluded. A host event supplies version/instance evidence.
            var snapshot = JsonSerializer.Serialize(new {
                serviceName = o.ServiceName, minimumLevel = o.MinimumLevel.ToString(), frameworkMinimumLevel = o.FrameworkMinimumLevel.ToString(),
                queueCapacity = o.QueueCapacity, importantQueueCapacity = o.ImportantQueueCapacity, batchSize = o.BatchSize, flushIntervalMs = o.FlushIntervalMs,
                retrySeconds = o.RetrySeconds, sqlTimeoutSeconds = o.SqlTimeoutSeconds, shutdownSeconds = o.ShutdownSeconds,
                fileSizeBytes = o.FileSizeBytes, maxDiskBytes = o.MaxDiskBytes, maxSqlRows = o.MaxSqlRows,
                fileRetentionDays = o.FileRetentionDays, retentionDays = o.RetentionDays, auditRetentionDays = o.AuditRetentionDays, cleanupBatchSize = o.CleanupBatchSize,
                locationFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(o.Directory + "\n" + o.OtlpEndpoint))),
                lowLevelSampleEvery = o.LowLevelSampleEvery, maxQueryDays = o.MaxQueryDays, maxExportRows = o.MaxExportRows, maxExportDays = o.MaxExportDays, otlpEnabled = o.OtlpEnabled
            });
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
            var previous = await db.AuditEvents.AsNoTracking().Where(x => x.Action == "system.diagnostics.configuration").OrderByDescending(x => x.Id).Select(x => x.DetailsJson).FirstOrDefaultAsync(ct);
            string? previousFingerprint = null;
            try { if (previous is not null) { using var document = JsonDocument.Parse(previous); previousFingerprint = document.RootElement.GetProperty("fingerprint").GetString(); } }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { }
            if (previousFingerprint != fingerprint)
            {
                db.AuditEvents.Add(new() { OwnerId = Guid.Empty, Action = "system.diagnostics.configuration", Result = "saved",
                    DetailsJson = JsonSerializer.Serialize(new { fingerprint, after = JsonSerializer.Deserialize<JsonElement>(snapshot) }) });
                await db.SaveChangesAsync(ct);
            }
            configurationRecorded = true;
        }
        var diagnosticBefore = DateTimeOffset.UtcNow.AddDays(-options.Value.RetentionDays);
        var auditBefore = DateTimeOffset.UtcNow.AddDays(-options.Value.AuditRetentionDays);
        // Separate autocommit batches; cancellation/timeout bounds total work, no long purge transaction.
        for (var batch = 0; batch < 100; batch++)
        {
            var ids = await db.Set<DiagnosticEvent>().Where(x => x.At < diagnosticBefore).OrderBy(x => x.At).Select(x => x.LogId).Take(options.Value.CleanupBatchSize).ToArrayAsync(ct);
            var auditIds = await db.AuditEvents.Where(x => x.At < auditBefore).OrderBy(x => x.At).Select(x => x.Id).Take(options.Value.CleanupBatchSize).ToArrayAsync(ct);
            if (ids.Length > 0) await db.Set<DiagnosticEvent>().Where(x => ids.Contains(x.LogId)).ExecuteDeleteAsync(ct);
            if (auditIds.Length > 0) await db.AuditEvents.Where(x => auditIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
            if (ids.Length < options.Value.CleanupBatchSize && auditIds.Length < options.Value.CleanupBatchSize) break;
        }
    }
}
