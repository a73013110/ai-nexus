using AiNexus.Data.App;
using AiNexus.Platform.Modules;
using System.Globalization;
using System.Data;
using System.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>使用自己的連線與交易寫入，絕不共用呼叫端的業務 DbContext 或交易，避免日誌寫入跟著業務交易一起回滾。</summary>
[Singleton<IDiagnosticStore>]
public sealed class DiagnosticStore(IServiceScopeFactory scopes, IOptions<DiagnosticOptions> options, DiagnosticHealth health) : IDiagnosticStore
{
    // EF 模型在程式執行期間不變，欄位與存取子只建一次；並行時重複建立也無妨。
    private (string Name, Type Type, Func<DiagnosticEvent, object?> Value)[]? mapped;
    public async Task WriteAsync(IReadOnlyList<DiagnosticEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;
        using var suppress = DiagnosticSuppression.Enter();
        using var ambient = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = options.Value;
        var connectionString = new SqlConnectionStringBuilder(db.Database.GetConnectionString()) { Enlist = false, ConnectTimeout = settings.SqlTimeoutSeconds }.ConnectionString;
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(ct);
        SqlCommand Command(string sql, SqlTransaction? transaction = null) => new(sql, connection, transaction) { CommandTimeout = settings.SqlTimeoutSeconds };
        // 列數取自系統中繼資料，避免對持續成長的表做 COUNT(*)。容量只是近似的軟上限（並行批次與估計值可能略超出），
        // 這裡不會為了騰空間而提前清除，保存期清理由 DiagnosticRetentionTask 負責。
        await using (var count = Command("SELECT COALESCE(SUM([rows]),0) FROM sys.partitions WHERE [object_id]=OBJECT_ID(N'diagnostics.DiagnosticEvents') AND [index_id] IN (0,1);"))
        {
            var rows = Convert.ToInt64(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); Interlocked.Exchange(ref health.EstimatedSqlRows, rows);
            if (rows + events.Count > settings.MaxSqlRows)
            {
                var ids = events.Select(x => x.LogId).Distinct().ToArray();
                await using var exists = Command("SELECT COUNT(*) FROM [diagnostics].[DiagnosticEvents] WHERE [LogId] IN (" + string.Join(',', ids.Select((_, i) => "@id" + i)) + ");");
                for (var i = 0; i < ids.Length; i++) exists.Parameters.AddWithValue("@id" + i, ids[i]);
                var existing = Convert.ToInt64(await exists.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
                if (ids.Length > existing && rows + ids.Length - existing > settings.MaxSqlRows) throw new DiagnosticCapacityException();
            }
        }
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var mapped = this.mapped ??= Columns(db.Model);
        var columns = string.Join(",", mapped.Select(x => "[" + x.Name + "]"));
        await using (var create = Command("SELECT TOP (0) " + columns + " INTO #LogBatch FROM [diagnostics].[DiagnosticEvents];", transaction))
            await create.ExecuteNonQueryAsync(ct);
        var table = new DataTable();
        foreach (var column in mapped) table.Columns.Add(column.Name, column.Type);
        foreach (var item in events.DistinctBy(x => x.LogId)) table.Rows.Add([.. mapped.Select(x => x.Value(item) ?? DBNull.Value)]);
        using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction) { DestinationTableName = "#LogBatch", BatchSize = settings.BatchSize, BulkCopyTimeout = settings.SqlTimeoutSeconds })
        {
            foreach (var column in mapped) bulk.ColumnMappings.Add(column.Name, column.Name);
            await bulk.WriteToServerAsync(table, ct);
        }
        await using (var insert = Command("INSERT INTO [diagnostics].[DiagnosticEvents] (" + columns + ") SELECT " + string.Join(",", mapped.Select(x => "b.[" + x.Name + "]")) +
            " FROM #LogBatch b WHERE NOT EXISTS (SELECT 1 FROM [diagnostics].[DiagnosticEvents] d WITH (UPDLOCK,HOLDLOCK) WHERE d.[LogId]=b.[LogId]);", transaction))
            await insert.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }
    /// <summary>
    /// 批次寫入的欄位取自 EF 模型（欄名與資料表對應一致），值以 EF 編譯好的存取子讀取並轉成資料庫型別（例如 <see cref="LogLevel"/> 轉 int），
    /// 不以反射逐格讀屬性。
    /// </summary>
    private static (string Name, Type Type, Func<DiagnosticEvent, object?> Value)[] Columns(IModel model) =>
    [
        .. model.FindEntityType(typeof(DiagnosticEvent))!.GetProperties().Select(property =>
        {
            var getter = property.GetGetter();
            var converter = property.GetTypeMapping().Converter;
            var type = converter?.ProviderClrType ?? property.ClrType;
            return (property.GetColumnName(), Nullable.GetUnderlyingType(type) ?? type,
                (Func<DiagnosticEvent, object?>)(item => getter.GetClrValue(item) is { } value ? converter?.ConvertToProvider(value) ?? value : null));
        }),
    ];
}
