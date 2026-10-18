using AiNexus.Data.App;
using System.Globalization;
using System.Data;
using System.Reflection;
using System.Transactions;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Diagnostics;

/// <summary>使用自己的連線與交易寫入，絕不共用呼叫端的業務 DbContext 或交易，避免日誌寫入跟著業務交易一起回滾。</summary>
public sealed class DiagnosticStore(IServiceScopeFactory scopes, IOptions<DiagnosticOptions> options, DiagnosticHealth health, TimeProvider clock) : IDiagnosticStore
{
    private static readonly PropertyInfo[] Columns = typeof(DiagnosticEvent).GetProperties();
    private bool configurationRecorded;
    public async Task WriteAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;
        using var suppress = DiagnosticSuppression.Enter();
        using var ambient = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = options.Value;
        var connectionString = new SqlConnectionStringBuilder(db.Database.GetConnectionString()) { Enlist = false, ConnectTimeout = settings.SqlTimeoutSeconds }.ConnectionString;
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(ct);
        // 列數取自系統中繼資料，避免對持續成長的表做 COUNT(*)。容量只是近似的軟上限（並行批次與估計值可能略超出），
        // 這裡不會為了騰空間而提前清除，保留期清理由 CleanupAsync 負責。
        await using (var count = new SqlCommand("SELECT COALESCE(SUM([rows]),0) FROM sys.partitions WHERE [object_id]=OBJECT_ID(N'diagnostics.DiagnosticEvents') AND [index_id] IN (0,1);", connection) { CommandTimeout = settings.SqlTimeoutSeconds })
        {
            var rows = Convert.ToInt64(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); Interlocked.Exchange(ref health.EstimatedSqlRows, rows);
            if (rows + events.Count > settings.MaxSqlRows)
            {
                var ids = events.Select(x => x.LogId).Distinct().ToArray();
                await using var exists = new SqlCommand("SELECT COUNT(*) FROM [diagnostics].[DiagnosticEvents] WHERE [LogId] IN (" + string.Join(',', ids.Select((_, i) => "@id" + i)) + ");", connection) { CommandTimeout = settings.SqlTimeoutSeconds };
                for (var i = 0; i < ids.Length; i++) exists.Parameters.AddWithValue("@id" + i, ids[i]);
                var existing = Convert.ToInt64(await exists.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
                if (ids.Length > existing && rows + ids.Length - existing > settings.MaxSqlRows) throw new DiagnosticCapacityException();
            }
        }
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var columns = string.Join(",", Columns.Select(x => "[" + x.Name + "]"));
        await using (var create = new SqlCommand("SELECT TOP (0) " + columns + " INTO #LogBatch FROM [diagnostics].[DiagnosticEvents];", connection, transaction) { CommandTimeout = settings.SqlTimeoutSeconds })
            await create.ExecuteNonQueryAsync(ct);
        var table = new DataTable();
        foreach (var column in Columns) table.Columns.Add(column.Name, column.PropertyType == typeof(LogLevel) ? typeof(int) : Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType);
        foreach (var item in events.DistinctBy(x => x.LogId)) table.Rows.Add(Columns.Select(x => x.GetValue(item) is { } value ? value is LogLevel l ? (object)(int)l : value : DBNull.Value).ToArray());
        using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction) { DestinationTableName = "#LogBatch", BatchSize = settings.BatchSize, BulkCopyTimeout = settings.SqlTimeoutSeconds })
        {
            foreach (var column in Columns) bulk.ColumnMappings.Add(column.Name, column.Name);
            await bulk.WriteToServerAsync(table, ct);
        }
        await using (var insert = new SqlCommand("INSERT INTO [diagnostics].[DiagnosticEvents] (" + columns + ") SELECT " + string.Join(",", Columns.Select(x => "b.[" + x.Name + "]")) +
            " FROM #LogBatch b WHERE NOT EXISTS (SELECT 1 FROM [diagnostics].[DiagnosticEvents] d WITH (UPDLOCK,HOLDLOCK) WHERE d.[LogId]=b.[LogId]);", connection, transaction) { CommandTimeout = settings.SqlTimeoutSeconds })
            await insert.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task CleanupAsync(CancellationToken ct)
    {
        using var suppress = DiagnosticSuppression.Enter();
        using var ambient = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.SetCommandTimeout(options.Value.SqlTimeoutSeconds);
        if (!configurationRecorded)
        {
            var o = options.Value;
            // 設定變更只稽核一次，且要等 SQL 可用後才寫。路徑與匯出端 URL 刻意排除，避免把內部位置寫進稽核；
            // 版本與實例證據由主機事件提供。
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
            // 舊紀錄格式無法解析時視為沒有先前指紋，會多寫一筆稽核，但不會阻擋清理。
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { }
            if (previousFingerprint != fingerprint)
            {
                db.AuditEvents.Add(new() { OwnerId = Guid.Empty, Action = "system.diagnostics.configuration", Result = "saved",
                    DetailsJson = JsonSerializer.Serialize(new { fingerprint, after = JsonSerializer.Deserialize<JsonElement>(snapshot) }) });
                await db.SaveChangesAsync(ct);
            }
            configurationRecorded = true;
        }
        var diagnosticBefore = clock.GetUtcNow().AddDays(-options.Value.RetentionDays);
        var auditBefore = clock.GetUtcNow().AddDays(-options.Value.AuditRetentionDays);
        // 每批各自 autocommit，總工作量由取消與逾時限制；刻意不用一筆長交易清除。
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
