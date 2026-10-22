using System.Data.Common;
using AiNexus.Features.Admin.Integrations;
using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Sql;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Staff.Attendance.Gd;

/// <summary>
/// 出勤唯讀查詢公文系統（GD）的唯一入口；連線用 Integrations 宣告的 <see cref="GdDatabase"/>，逾時沿用 <c>Integrations:Gd:CommandTimeoutSeconds</c>。
/// GD 是別人的系統，連不上不能拖垮出勤頁：未設定，或連線、登入、逾時、結構變動等資料庫錯誤時回傳 <see langword="null"/>，由呼叫端決定降級還是拒絕。
/// 取消與非資料庫的例外（程式錯誤、欄位型別對不上）照常拋出。
/// </summary>
/// <remarks>
/// 日誌只在狀態轉換時記一次（連不上、恢復）：GD 停機期間每次開出勤頁都會呼叫，逐次記錄只會洗版。狀態在記憶體，重啟後歸零，所以註冊為單例。
/// </remarks>
public sealed partial class GdAccess(ISqlDatabase<GdDatabase> database, IOptions<DatabasesOptions> databases, IOptions<IntegrationsOptions> integrations, ILogger<GdAccess> logger)
{
    // SQL Server 單一命令最多 2100 個參數，Dapper 把 IN 清單逐項展開成參數；同一個清單在 SQL 出現多次仍共用同一組參數。
    private const int BatchSize = 500;

    private int unavailable;

    /// <summary>執行唯讀查詢；GD 不可用時為 <see langword="null"/>（與「沒有資料」的空清單不同）。</summary>
    public async Task<IReadOnlyList<T>?> QueryAsync<T>(string sql, object? parameters, CancellationToken ct)
    {
        if (!databases.Value.For(GdDatabase.Name).Configured)
        {
            MarkUnavailable("NotConfigured");
            return null;
        }
        try
        {
            var rows = await database.QueryAsync<T>(sql, parameters, integrations.Value.Gd.CommandTimeoutSeconds, ct);
            if (Interlocked.Exchange(ref unavailable, 0) == 1) LogRestored(logger);
            return rows;
        }
        catch (DbException ex)
        {
            // 查詢進行中被取消時，SqlClient 丟的是 SqlException 而不是 OperationCanceledException；那是呼叫端放棄，不是 GD 壞了。
            ct.ThrowIfCancellationRequested();
            MarkUnavailable(ex.GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// <c>IN</c> 清單可能很長的查詢：<paramref name="keys"/> 分批代入、結果串接；<paramref name="parameters"/> 把一批鍵組成該批的參數物件。
    /// 任何一批失敗都回傳 <see langword="null"/>，不回傳只有部分人的結果。
    /// </summary>
    public async Task<IReadOnlyList<T>?> QueryInAsync<T>(string sql, IEnumerable<string> keys, Func<string[], object> parameters, CancellationToken ct)
    {
        var rows = new List<T>();
        foreach (var batch in keys.Chunk(BatchSize))
        {
            if (await QueryAsync<T>(sql, parameters(batch), ct) is not { } found) return null;
            rows.AddRange(found);
        }
        return rows;
    }

    private void MarkUnavailable(string errorType)
    {
        if (Interlocked.Exchange(ref unavailable, 1) == 0) LogUnavailable(logger, errorType);
    }

    [LoggerMessage(EventId = 3407, EventName = "attendance.gd_unavailable", Level = LogLevel.Warning, Message = "GD cannot be queried ({ErrorType}); leave and travel sheets are unavailable until it is reachable.")]
    private static partial void LogUnavailable(ILogger logger, string errorType);

    [LoggerMessage(EventId = 3408, EventName = "attendance.gd_restored", Level = LogLevel.Information, Message = "GD can be queried again.")]
    private static partial void LogRestored(ILogger logger);
}
