using System.Data;
using System.Data.Common;
using AiNexus.Features.Admin.Integrations.Gd;
using AiNexus.Platform.Errors;
using Microsoft.Data.SqlClient;

namespace AiNexus.Features.Office.Attendance.Gd;

/// <summary>GD 假日一天的值：<c>AM</c>／<c>PM</c> 為 true＝該半天放假，<c>hReMark</c> 是備註。</summary>
public sealed record GdCalendarValues(bool MorningOff, bool AfternoonOff, string? Remark);

/// <summary>
/// 回寫 GD 假日的唯一入口（ADR 0018 的例外寫入），連線、逾時與唯讀設定都取自查詢用的 <see cref="GdAccess"/>（同一個 <c>Databases:Gd</c>）。
/// 寫入範圍由這裡的 SQL 保證，不靠資料庫帳號：只 <c>UPDATE</c> 既有日期的 <c>AM</c>、<c>PM</c>、<c>hReMark</c> 與 <c>UserId</c>，不新增、不刪除。
/// 回傳的 <see cref="GdCalendarEdit"/> 仍持有交易與列鎖，呼叫端寫完本系統的稽核才提交，見 <see cref="GdCalendarEdit"/>。
/// </summary>
/// <remarks>
/// <c>ISqlDatabase</c> 每次呼叫各開一條連線、不支援跨命令的交易，
/// 而「鎖定讀前值 → 更新 → 讀回 → 等稽核寫完再提交」必須在同一條連線、同一個交易，所以這裡直接用 SqlClient。
/// </remarks>
public sealed partial class GdCalendarWriter(GdAccess gd, ILogger<GdCalendarWriter> logger)
{
    // hDate 是 datetime，以整天的區間比對：公文系統寫入的日期理應是午夜，萬一帶了時間也不會漏掉。同一天有多列時一起更新，前值取第一列。
    // UPDLOCK＋HOLDLOCK 鎖住這一天到提交為止：並發的兩次修改依序進行，各自的稽核前值都是真正的前一個狀態。
    private const string LockSql = """
        SELECT [AM], [PM], [hReMark], [UserId] FROM [dbo].[Pub_Days] WITH (UPDLOCK, HOLDLOCK)
        WHERE [hDate] >= @Day AND [hDate] < @Next ORDER BY [hDate];
        """;

    private const string UpdateSql = "UPDATE [dbo].[Pub_Days] SET [AM] = @Am, [PM] = @Pm, [hReMark] = @Remark, [UserId] = @UserId WHERE [hDate] >= @Day AND [hDate] < @Next;";

    private const string ReadBackSql = "SELECT [hReMark] FROM [dbo].[Pub_Days] WHERE [hDate] >= @Day AND [hDate] < @Next;";

    /// <summary><c>Pub_Days.UserId</c> 與 <c>Sys_User.ID</c> 都是 varchar(20)。</summary>
    public const int UserIdMaxLength = 20;

    /// <summary>字串截斷：2628 是 SQL Server 2019 起的訊息，8152 是較舊的版本。</summary>
    private static readonly int[] TruncationErrors = [2628, 8152];

    /// <summary>
    /// 不必連 GD 就能判定的拒絕：未設定 <c>Databases:Gd</c> 回 <c>attendance_gd_unavailable</c>（與查詢一致），
    /// 設為 <c>ReadOnly=true</c> 回 <c>attendance_gd_read_only</c>。
    /// </summary>
    public Result CanWrite() => !gd.Configured ? AttendanceErrors.GdUnavailable : gd.ReadOnly ? AttendanceErrors.GdReadOnly : Result.Success;

    /// <summary>
    /// 鎖定並更新 <paramref name="day"/>，修改者寫 <paramref name="gdUserId"/>；成功時交易尚未提交。
    /// 失敗（未設定或唯讀、日期不存在、備註存不下、連不上或沒有權限）時交易已回滾或根本沒開始，GD 沒有任何變動。
    /// </summary>
    public async Task<Result<GdCalendarEdit>> UpdateAsync(DateOnly day, GdCalendarValues values, string gdUserId, CancellationToken ct)
    {
        if (CanWrite().Error is { } refused) return refused;
        SqlConnection? connection = null;
        SqlTransaction? transaction = null;
        try
        {
            connection = await gd.OpenAsync(ct);
            transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            GdCalendarValues? before = null;
            string? previousUserId = null;
            await using (var select = Command(LockSql, connection, transaction, day))
            await using (var reader = await select.ExecuteReaderAsync(ct))
                if (await reader.ReadAsync(ct))
                {
                    before = new(reader.GetBoolean(0), reader.GetBoolean(1), reader.IsDBNull(2) ? null : reader.GetString(2));
                    previousUserId = reader.IsDBNull(3) ? null : reader.GetString(3).Trim();
                }
            if (before is null) return await FailAsync(AttendanceErrors.CalendarDayNotFound);

            await using var update = Command(UpdateSql, connection, transaction, day);
            update.Parameters.Add("@Am", SqlDbType.Bit).Value = values.MorningOff;
            update.Parameters.Add("@Pm", SqlDbType.Bit).Value = values.AfternoonOff;
            // 以 nvarchar 傳入，由 SQL Server 依 hReMark 欄位的定序轉成 varchar：存不存得下只有 GD 自己的定序說了算（見下方讀回比對）。
            update.Parameters.Add("@Remark", SqlDbType.NVarChar, 4000).Value = (object?)values.Remark ?? DBNull.Value;
            update.Parameters.Add("@UserId", SqlDbType.VarChar, UserIdMaxLength).Value = gdUserId;
            try { await update.ExecuteNonQueryAsync(ct); }
            catch (SqlException ex) when (TruncationErrors.Contains(ex.Number)) { return await FailAsync(AttendanceErrors.CalendarRemarkInvalid); }

            // hReMark 是 varchar(255)：長度是「位元組」，非 ASCII 字的位元組數取決於欄位定序的字碼頁（Big5 中文 2 位元組、UTF-8 定序 3 位元組），
            // 字碼頁沒有的字（罕用字、表情符號）會被靜默換成問號。本系統不知道 GD 的定序，所以不在程式裡估算，
            // 而是寫入後在同一個交易讀回、與要求的字串逐字比對：超長由 SQL Server 以截斷錯誤擋下，變了樣就回滾，GD 不會存進走樣的備註。
            await using var readBack = Command(ReadBackSql, connection, transaction, day);
            await using (var reader = await readBack.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct))
                    if (!string.Equals(reader.IsDBNull(0) ? null : reader.GetString(0), values.Remark, StringComparison.Ordinal))
                    {
                        await reader.CloseAsync();
                        return await FailAsync(AttendanceErrors.CalendarRemarkInvalid);
                    }

            var edit = new GdCalendarEdit(connection, transaction, before, previousUserId, this);
            connection = null;
            transaction = null;
            return edit;
        }
        catch (DbException ex)
        {
            // 執行中被取消時 SqlClient 丟的是 SqlException；那是呼叫端放棄，不是 GD 壞了。
            ct.ThrowIfCancellationRequested();
            LogWriteFailed(logger, ex.GetType().Name, (ex as SqlException)?.Number ?? 0);
            return AttendanceErrors.GdUnavailable;
        }
        finally
        {
            // 走到這裡仍持有的交易都還沒提交，Dispose 即回滾。
            if (transaction is not null) await transaction.DisposeAsync();
            if (connection is not null) await connection.DisposeAsync();
        }

        async Task<Result<GdCalendarEdit>> FailAsync(Error error)
        {
            await transaction!.RollbackAsync(CancellationToken.None);
            return error;
        }
    }

    internal void CommitFailed(DbException ex) => LogCommitFailed(logger, ex.GetType().Name, (ex as SqlException)?.Number ?? 0);

    private SqlCommand Command(string sql, SqlConnection connection, SqlTransaction transaction, DateOnly day)
    {
        var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = gd.CommandTimeoutSeconds };
        command.Parameters.Add("@Day", SqlDbType.DateTime).Value = day.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@Next", SqlDbType.DateTime).Value = day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return command;
    }

    [LoggerMessage(EventId = 3409, EventName = "attendance.calendar_write_failed", Level = LogLevel.Warning, Message = "GD calendar write-back failed ({ErrorType}, {SqlNumber}); nothing was changed in GD.")]
    private static partial void LogWriteFailed(ILogger logger, string errorType, int sqlNumber);

    [LoggerMessage(EventId = 3410, EventName = "attendance.calendar_commit_failed", Level = LogLevel.Error, Message = "GD calendar commit failed after the audit was recorded ({ErrorType}, {SqlNumber}); the change may not have been applied.")]
    private static partial void LogCommitFailed(ILogger logger, string errorType, int sqlNumber);
}

/// <summary>
/// 已鎖定並更新、尚未提交的 GD 假日修改。呼叫端先在本系統寫稽核，再 <see cref="CommitAsync"/>；沒有提交就 Dispose 等於回滾。
/// 這個順序讓「GD 改了卻沒有稽核」不會發生；反過來，提交本身失敗時已有一筆 saved 稽核，呼叫端要補一筆失敗紀錄。
/// </summary>
public sealed class GdCalendarEdit : IAsyncDisposable
{
    private readonly SqlConnection connection;
    private readonly SqlTransaction transaction;
    private readonly GdCalendarWriter writer;

    internal GdCalendarEdit(SqlConnection connection, SqlTransaction transaction, GdCalendarValues before, string? previousUserId, GdCalendarWriter writer)
    {
        this.connection = connection;
        this.transaction = transaction;
        this.writer = writer;
        Before = before;
        PreviousUserId = previousUserId;
    }

    public GdCalendarValues Before { get; }

    /// <summary>修改前的 <c>UserId</c>（公文系統自己改的是 <c>sys</c>）。</summary>
    public string? PreviousUserId { get; }

    /// <summary>
    /// 提交 GD 交易。稽核已寫入後才呼叫，所以不接受取消：放棄提交只會讓稽核與 GD 不一致。
    /// 失敗時回傳 false；網路在提交途中中斷時 GD 可能其實已提交，結果不確定，由使用者重新讀取確認。
    /// </summary>
    public async Task<bool> CommitAsync()
    {
        try
        {
            await transaction.CommitAsync(CancellationToken.None);
            return true;
        }
        catch (DbException ex)
        {
            writer.CommitFailed(ex);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }
}
