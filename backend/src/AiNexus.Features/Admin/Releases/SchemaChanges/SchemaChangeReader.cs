using System.Diagnostics;
using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Admin.Releases.SchemaChanges;

/// <summary>
/// 與這一版應有結構不同的物件。<paramref name="Change"/> 為 <c>added</c>、<c>modified</c>、<c>removed</c>；
/// <paramref name="ChangedAt"/> 取 <c>modify_date</c>／<c>create_date</c>，只當參考（ADR 0028），刪除沒有時間。
/// </summary>
public sealed record SchemaChangeDto(string SchemaName, string Name, string Type, string Change, DateTimeOffset? ChangedAt);

/// <summary>一個物件應有與實際的定義本文；不存在的一邊為 null。資料表的定義是欄位、索引與約束組成的固定格式，不是可執行的 DDL。</summary>
public sealed record SchemaObjectDto(string Type, string? Expected, string? Actual);

/// <summary>物件目前在資料庫裡的定義。</summary>
public sealed record SchemaDefinitionDto(string SchemaName, string Name, string Type, string Definition);

/// <summary>
/// 呼叫 <c>releases</c> 的 procedure：雜湊在伺服器端計算、只傳回有差異的列；定義本文在打開差異時才讀。
/// update.sql 的前置檢查用同一支，所以網站與更新包對「異動」的判定一致。
/// </summary>
internal sealed partial class SchemaChangeReader(ISqlDatabase<AppDbContext> sql, ILogger<SchemaChangeReader> logger)
{
    /// <summary>快照是空的（還沒有任何 update.sql 或建庫腳本寫入）時回傳空清單；是否有快照由 <see cref="HasSnapshotAsync"/> 分辨。</summary>
    public async Task<IReadOnlyList<SchemaChangeDto>> ListAsync(CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var changes = await sql.QueryProcedureAsync<SchemaChangeDto>("releases.usp_ListSchemaChanges", cancellationToken: ct);
        // 讀的是整個資料庫的 metadata，耗時隨物件數成長；記下來才知道監控與每日檢查的成本。
        var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LogChangesRead(logger, changes.Count, elapsed);
        return changes;
    }

    public Task<bool> HasSnapshotAsync(CancellationToken ct)
        => sql.QuerySingleAsync<bool>("SELECT CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM [releases].[SchemaObjects]) THEN 1 ELSE 0 END)", cancellationToken: ct);

    public async Task<SchemaObjectDto?> GetAsync(string schema, string name, CancellationToken ct)
        => (await sql.QueryProcedureAsync<SchemaObjectDto>("releases.usp_GetSchemaObject", new { SchemaName = schema, Name = name }, cancellationToken: ct)).SingleOrDefault();

    /// <summary>
    /// 多個物件目前的定義，一次計算：<c>usp_GetSchemaObject</c> 每呼叫一次就算一次整個資料庫的快照，逐一呼叫的成本隨物件數倍增。
    /// 名稱以 OPENJSON 傳入，兩邊都轉成資料庫預設 collation 再比對（目錄檢視的名稱是另一種 collation）。
    /// </summary>
    public async Task<IReadOnlyList<SchemaDefinitionDto>> DefinitionsAsync(IEnumerable<SchemaChangeDto> objects, CancellationToken ct)
    {
        var keys = JsonSerializer.Serialize(objects.Select(x => new { s = x.SchemaName, n = x.Name }));
        return await sql.QueryAsync<SchemaDefinitionDto>("""
            SELECT a.[SchemaName], a.[Name], a.[Type], a.[Definition]
            FROM [releases].[udf_SchemaObjects]() AS a
            JOIN OPENJSON(@keys) WITH ([SchemaName] sysname '$.s', [Name] sysname '$.n') AS k
              ON k.[SchemaName] COLLATE DATABASE_DEFAULT = a.[SchemaName] COLLATE DATABASE_DEFAULT
             AND k.[Name] COLLATE DATABASE_DEFAULT = a.[Name] COLLATE DATABASE_DEFAULT
            ORDER BY a.[SchemaName], a.[Name];
            """, new { keys }, cancellationToken: ct);
    }

    [LoggerMessage(EventId = 3500, EventName = "releases.schema_changes_read", Level = LogLevel.Information,
        Message = "Schema changes read: {Count} objects differ from the release snapshot in {ElapsedMs} ms.")]
    private static partial void LogChangesRead(ILogger logger, int count, long elapsedMs);
}
