using System.Globalization;
using System.Text.RegularExpressions;
using AiNexus.Platform.Data.External;

namespace AiNexus.Features.Platform.Gd;

public enum GdFlowStepState
{
    /// <summary>已處理（<c>FLOW_HISTORY</c>，結案後在 <c>FLOW_BACKUP_&lt;民國年&gt;</c>）。</summary>
    Done,

    /// <summary>目前停在這一關（<c>FLOW_CURRENT</c>）；同時有多列是並簽。</summary>
    Current,

    /// <summary>還沒輪到（<c>FLOW_NEXT</c>）。</summary>
    Pending,
}

/// <summary>
/// 簽核流程的一關。<paramref name="Action"/> 是 GD 的 <c>STATUS</c> 轉成的畫面字樣（見 <see cref="GdFlowReader.ActionOf"/>）；
/// <paramref name="Agent"/> 是 <c>ORIGIN_ID</c> 指的代理人（GD 把代理簽核的帳號記在這裡），沒有代理時為 null。
/// <paramref name="At"/> 是 <c>TIMERECORD</c>（台灣時間）：已處理是簽核時間，目前關卡是送達時間。
/// </summary>
public sealed record GdFlowStep(GdFlowStepState State, string Action, GdPerson? Person, string? Agent, DateTimeOffset? At, string? Memo);

/// <summary>
/// 讀一張表單或公文（<c>KEY_ID</c>＝單號）的簽核流程，給所有要顯示 GD 流程的模組共用。四張流程表結構相同，以 UNION ALL 一次查完。
/// GD 不可用或查詢被拒時回傳 null，與「沒有任何關卡」（空清單）不同。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>流程結束後，GD 把該單的歷程搬到 <c>FLOW_BACKUP_&lt;民國年&gt;</c>，年取單號第 2～4 碼（GD <c>EfFlowUtil</c> 的 <c>keyID.substring(1,4)</c>）。
/// 表名不能用參數帶入，所以只接受三位數字組成表名，並先以 <c>OBJECT_ID</c> 確認存在（沒有權限時也查不到，等同不存在），不存在就只查其他三張表。</item>
/// <item>只有流程中的單（<c>active</c>）才讀目前與未到的關卡：結案後 <c>FLOW_CURRENT</c>、<c>FLOW_NEXT</c> 可能還留著終結列，當成「還在等」會誤導。</item>
/// <item>同一階段依 <c>FLOW_NO</c>（GD 寫成補零的三位數字串）排序；流程表沒有外鍵，人員對不到時只有動作與時間。</item>
/// </list>
/// </remarks>
public sealed partial class GdFlowReader(ExternalDatabase<GdDatabase> gd)
{
    private const string Columns = "[FLOW_NO], [TIMERECORD], [STATUS], [PERSON_ID], [ORIGIN_ID], CAST([MEMO] AS nvarchar(max))";

    public async Task<IReadOnlyList<GdFlowStep>?> ReadAsync(string desNo, bool active, CancellationToken ct)
    {
        var backup = BackupTable(desNo);
        var sql = backup is null ? Query(null) : $"""
            IF OBJECT_ID(N'[dbo].[{backup}]', N'U') IS NULL
            {Query(null)}
            ELSE
            {Query(backup)}
            """;
        var rows = await gd.QueryAsync<StepRow>(sql, new { DesNo = desNo, Active = active }, ct);
        return rows?.Select(Step).ToList();
    }

    /// <summary>單號的流程備份表名；單號第 2～4 碼不是數字時為 null（不猜，只查其他三張表）。</summary>
    public static string? BackupTable(string desNo) => BackupYear().Match(desNo) is { Success: true } match ? "FLOW_BACKUP_" + match.Groups[1].Value : null;

    /// <summary>
    /// GD 的 <c>STATUS</c> 轉成畫面字樣：送出時 GD 寫「待處理表單」（承辦人的待辦匣名稱），對看單的人來說是「送出」；
    /// 其他（「請假代理人」「串簽」「決行」等）本來就是關卡名稱，照原字顯示，沒見過的字也一樣。
    /// </summary>
    public static string ActionOf(string? status) => GdPeople.Text(status) switch
    {
        null => "簽核",
        "待處理表單" => "送出",
        var text => text,
    };

    // 用衍生資料表而不是 CTE：IF 後面接 WITH 需要分號，容易寫錯。
    private static string Query(string? backup) => $$"""
        SELECT f.Stage, f.RecordedAt, f.Status, f.Memo, p.Name, p.Unit, p.Title, a.[NAME] AS AgentName
        FROM (
            SELECT 0, {{Columns}} FROM [dbo].[FLOW_HISTORY] WHERE [KEY_ID] = @DesNo
            {{(backup is null ? "" : $"UNION ALL SELECT 0, {Columns} FROM [dbo].[{backup}] WHERE [KEY_ID] = @DesNo")}}
            UNION ALL SELECT 1, {{Columns}} FROM [dbo].[FLOW_CURRENT] WHERE [KEY_ID] = @DesNo AND @Active = 1
            UNION ALL SELECT 2, {{Columns}} FROM [dbo].[FLOW_NEXT] WHERE [KEY_ID] = @DesNo AND @Active = 1
        ) f (Stage, FlowNo, RecordedAt, Status, PersonId, OriginId, Memo)
        OUTER APPLY ({{GdPeople.Apply(GdPeople.UserIdOf("f.PersonId"), GdPeople.DepNoOf("f.PersonId"))}}) p
        LEFT JOIN [dbo].[Sys_User] a ON f.OriginId <> '' AND a.[ID] = {{GdPeople.UserIdOf("f.OriginId")}}
        ORDER BY f.Stage, TRY_CAST(f.FlowNo AS int), f.FlowNo
        """;

    private static GdFlowStep Step(StepRow row) => new((GdFlowStepState)row.Stage, ActionOf(row.Status), GdPeople.Person(row.Name, row.Unit, row.Title),
        GdPeople.Text(row.AgentName), RecordedAt(row.RecordedAt), GdPeople.Text(row.Memo));

    // GD 以 REPLACE(CONVERT(varchar(19), GETDATE(), 120), '-', '/') 寫入，是台灣時間的字串；解析不了就當沒有時間，不讓整段流程失敗。
    private static DateTimeOffset? RecordedAt(string? value)
        => DateTime.TryParseExact(value?.Trim(), ["yyyy/MM/dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm", "yyyy/MM/dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? new DateTimeOffset(time, TimeSpan.FromHours(8)) : null;

    [GeneratedRegex(@"^[A-Za-z]([0-9]{3})")]
    private static partial Regex BackupYear();

    private sealed class StepRow
    {
        public int Stage { get; init; }
        public string? RecordedAt { get; init; }
        public string? Status { get; init; }
        public string? Memo { get; init; }
        public string? Name { get; init; }
        public string? Unit { get; init; }
        public string? Title { get; init; }
        public string? AgentName { get; init; }
    }
}
