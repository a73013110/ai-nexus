using System.Collections.Frozen;
using System.Data.Common;
using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Integrations.Gd;

/// <summary>GD 查詢的結果：成功時 <paramref name="Rows"/> 有值；失敗時為 null，<paramref name="Failure"/> 是原因分類。</summary>
public sealed record GdRead<T>(IReadOnlyList<T>? Rows, GdFailure? Failure);

/// <summary>
/// 以程式查詢公文系統（GD）的唯一入口，出勤（假單、假日與回寫）與組織匯入共用：查詢走這裡，出勤的假日回寫也從這裡取得連線與設定，
/// 全部用 <see cref="GdDatabase"/>（<c>Databases:Gd</c>），逾時都是 <c>Integrations:Gd:CommandTimeoutSeconds</c>。
/// GD 是別人的系統，連不上不能拖垮呼叫端：未設定，或連線、登入、逾時、結構變動等資料庫錯誤時回傳 <see langword="null"/>，由呼叫端決定降級還是拒絕。
/// 取消與非資料庫的例外（程式錯誤、欄位型別對不上）照常拋出。每次存取的成敗記在 <see cref="Availability"/>，供監控（<see cref="GdMonitoring"/>）判定。
/// </summary>
/// <remarks>
/// 日誌只在狀態轉換時記一次（連不上、恢復；查詢被拒每段期間一次）：GD 停機期間每次開出勤頁都會呼叫，逐次記錄只會洗版。
/// 狀態在記憶體，重啟後歸零，所以註冊為單例。
/// </remarks>
public sealed partial class GdAccess(ISqlDatabase<GdDatabase> database, IOptions<DatabasesOptions> databases, IOptions<IntegrationsOptions> integrations,
    TimeProvider clock, ILogger<GdAccess> logger)
{
    // SQL Server 單一命令最多 2100 個參數，Dapper 把 IN 清單逐項展開成參數；同一個清單在 SQL 出現多次仍共用同一組參數。
    private const int BatchSize = 500;
    private const string ProbeSql = "SELECT 1";

    // 連得上但查詢本身被拒：207 欄位不存在、208 物件不存在、229／230 沒有物件或欄位權限、4104 無法繫結識別字。
    // 其餘錯誤（網路、登入 18456、資料庫離線 4060…）都當成整個不能用；-2 是用戶端逾時。
    private static readonly FrozenSet<int> RejectedErrors = FrozenSet.Create(207, 208, 229, 230, 4104);

    private readonly Lock gate = new();
    private GdAvailability availability = GdAvailability.Unknown;

    /// <summary>是否已設定 <c>Databases:Gd</c>。</summary>
    public bool Configured => Settings.Configured;

    /// <summary><c>Databases:Gd:ReadOnly</c>（預設 false）：部署選擇只讀 GD，假日回寫因此停用。連線同時帶 <c>ApplicationIntent=ReadOnly</c>。</summary>
    public bool ReadOnly => Settings.ReadOnly;

    /// <summary>每個 GD 命令的逾時秒數；查詢與回寫共用，等不到公文系統的鎖也算逾時。</summary>
    public int CommandTimeoutSeconds => integrations.Value.Gd.CommandTimeoutSeconds;

    private DatabaseOptions Settings => databases.Value.For(GdDatabase.Name);

    /// <summary>最近一次存取的結果。</summary>
    public GdAvailability Availability
    {
        get { lock (gate) return availability; }
    }

    /// <summary>執行唯讀查詢；GD 不可用時為 <see langword="null"/>（與「沒有資料」的空清單不同）。</summary>
    public async Task<IReadOnlyList<T>?> QueryAsync<T>(string sql, object? parameters, CancellationToken ct)
        => (await ReadAsync<T>(sql, parameters, ct)).Rows;

    /// <summary>同 <see cref="QueryAsync{T}"/>，失敗時另帶原因：給要把「未設定」「被拒」等原因告訴使用者的呼叫端，不必事後讀 <see cref="Availability"/>（可能已被並發的存取改掉）。</summary>
    public Task<GdRead<T>> ReadAsync<T>(string sql, object? parameters, CancellationToken ct)
        => RunAsync(token => database.QueryAsync<T>(sql, parameters, CommandTimeoutSeconds, token), ct, ct);

    /// <summary>
    /// <c>IN</c> 清單可能很長的查詢：<paramref name="keys"/> 分批代入、結果串接；<paramref name="parameters"/> 把一批鍵組成該批的參數物件。
    /// 任何一批失敗都回傳 <see langword="null"/>，不回傳只有部分人的結果。
    /// </summary>
    public async Task<IReadOnlyList<T>?> QueryInAsync<T>(string sql, IEnumerable<string> keys, Func<string[], object> parameters, CancellationToken ct)
        => (await ReadInAsync<T>(sql, keys, parameters, ct)).Rows;

    /// <summary>同 <see cref="QueryInAsync{T}"/>，失敗時另帶原因。</summary>
    public async Task<GdRead<T>> ReadInAsync<T>(string sql, IEnumerable<string> keys, Func<string[], object> parameters, CancellationToken ct)
    {
        var rows = new List<T>();
        foreach (var batch in keys.Chunk(BatchSize))
        {
            var read = await ReadAsync<T>(sql, parameters(batch), ct);
            if (read.Rows is not { } found) return read;
            rows.AddRange(found);
        }
        return new(rows, null);
    }

    /// <summary>
    /// 監控用的連線探測：<c>SELECT 1</c>，超過 <paramref name="timeout"/> 算逾時。結果與一般查詢一樣更新 <see cref="Availability"/> 與日誌。
    /// 只證明連得上，測不出某張表被拒，所以成功不會清掉查詢被拒的期間。
    /// </summary>
    public async Task ProbeAsync(TimeSpan timeout, CancellationToken ct)
    {
        // 時限用真實時間：它要趕在監控讀取本身的 5 秒逾時之前結束，和業務時間（TimeProvider）無關。
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        await RunAsync(token => database.QueryAsync<int>(ProbeSql, null, (int)Math.Ceiling(timeout.TotalSeconds), token), ct, limit.Token);
    }

    /// <summary>
    /// 開一條由呼叫端管理交易的 GD 連線，只給必須在同一個交易跨多個命令的假日回寫（出勤的 <c>GdCalendarWriter</c>）；查詢一律用 <see cref="QueryAsync{T}"/>。
    /// 連線字串與查詢同一個來源，未設定時呼叫端應先看 <see cref="Configured"/>。這條路不更新 <see cref="Availability"/>，失敗由呼叫端記錄。
    /// </summary>
    internal async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(databases.Value.ConnectionString(GdDatabase.Name));
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <param name="query">以傳入的 token 執行查詢。</param>
    /// <param name="ct">呼叫端的取消：照常拋出，不算 GD 的失敗。</param>
    /// <param name="limit">實際傳給查詢的 token；探測時多了時限，時限到了算逾時。一般查詢與 <paramref name="ct"/> 相同。</param>
    private async Task<GdRead<T>> RunAsync<T>(Func<CancellationToken, Task<IReadOnlyList<T>>> query, CancellationToken ct, CancellationToken limit)
    {
        if (!Configured) return Failed<T>(GdFailure.NotConfigured, nameof(GdFailure.NotConfigured), 0);
        try
        {
            var rows = await query(limit);
            Succeeded();
            return new(rows, null);
        }
        catch (DbException ex)
        {
            // 查詢進行中被取消時，SqlClient 丟的是 SqlException 而不是 OperationCanceledException；那是呼叫端放棄，不是 GD 壞了。
            ct.ThrowIfCancellationRequested();
            var number = (ex as SqlException)?.Number ?? 0;
            return Failed<T>(limit.IsCancellationRequested ? GdFailure.Timeout : Classify(number), ex.GetType().Name, number);
        }
        catch (OperationCanceledException ex) when (limit.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return Failed<T>(GdFailure.Timeout, ex.GetType().Name, 0);
        }
    }

    private static GdFailure Classify(int sqlNumber)
        => sqlNumber == -2 ? GdFailure.Timeout : RejectedErrors.Contains(sqlNumber) ? GdFailure.Rejected : GdFailure.Unavailable;

    private void Succeeded()
    {
        bool restored;
        lock (gate)
        {
            restored = availability.Outage is not null;
            availability = availability with { Outage = null, LastSuccessAt = clock.GetUtcNow() };
        }
        if (restored) LogRestored(logger);
    }

    private GdRead<T> Failed<T>(GdFailure reason, string errorType, int sqlNumber)
    {
        var now = clock.GetUtcNow();
        bool restored = false, report;
        lock (gate)
        {
            var previous = availability;
            if (reason == GdFailure.Rejected)
            {
                // 被拒代表連得上，連線層級的中斷到此結束；同一段被拒期間（間隔不到 RejectedWindow）只記一次。
                restored = previous.Outage is not null;
                report = !(now - previous.LastRejectedAt < GdAvailability.RejectedWindow);
                availability = previous with { Outage = null, LastFailureAt = now, LastFailure = reason, LastRejectedAt = now };
            }
            else
            {
                // 原因改變（例如未設定改成連不上、連不上改成逾時）也記，方便從日誌看出中斷的性質變了。
                report = previous.Outage != reason;
                availability = previous with { Outage = reason, LastFailureAt = now, LastFailure = reason };
            }
        }
        if (restored) LogRestored(logger);
        if (report && reason == GdFailure.Rejected) LogRejected(logger, errorType, sqlNumber);
        else if (report) LogUnavailable(logger, reason.ToString(), errorType, sqlNumber);
        return new(null, reason);
    }

    [LoggerMessage(EventId = 3407, EventName = "integrations.gd_unavailable", Level = LogLevel.Warning, Message = "GD cannot be queried ({Reason}, {ErrorType}, {SqlNumber}); features that read GD are unavailable until it is reachable.")]
    private static partial void LogUnavailable(ILogger logger, string reason, string errorType, int sqlNumber);

    [LoggerMessage(EventId = 3408, EventName = "integrations.gd_restored", Level = LogLevel.Information, Message = "GD can be queried again.")]
    private static partial void LogRestored(ILogger logger);

    [LoggerMessage(EventId = 3411, EventName = "integrations.gd_query_rejected", Level = LogLevel.Warning, Message = "GD rejected a query ({ErrorType}, {SqlNumber}); a table, column or grant the GD account needs is missing.")]
    private static partial void LogRejected(ILogger logger, string errorType, int sqlNumber);
}
