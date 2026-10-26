using System.Text.RegularExpressions;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Organization;
using AiNexus.Features.Platform.Gd;
using AiNexus.Features.Platform.RemoteFiles;
using AiNexus.Platform.Data.External;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Forms;

/// <summary>
/// 一張請假單或出差單的完整內容、簽核流程與附件（出勤紀錄點單號開啟）。權限與出勤紀錄相同：看得到申請人出勤紀錄的人，就看得到這張單與它的附件。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>只開放出勤紀錄會列出的單（流程中、核准），暫存、退回、銷單照樣回 404；單號不存在、範圍外也是 404，不讓人以單號猜出別人有沒有請假。</item>
/// <item>申請人以 <see cref="GdAccounts.UsersOfAsync"/> 對回使用者後套資料範圍；對不到使用者的單，出勤紀錄本來就列不出來，只有全公司範圍看得到。</item>
/// <item>流程與附件各自降級：讀不到時欄位為 null，表單本身照常回傳。附件只回短期憑證（<see cref="RemoteFileTickets"/>），
/// 憑證在範圍檢查之後才發。</item>
/// </list>
/// </remarks>
internal sealed partial class GetGdForm(ExternalDatabase<GdDatabase> gd, GdAccounts accounts, GdFlowReader flows, GdAttachments attachments,
    RemoteFileTickets tickets, IDataScope scopes, OrganizationDirectory organization)
{
    // 「待處理表單」等 GD 字樣不在這裡；SQL 只取欄位，顯示規則在 C#。喪假（G1）才有 BereaveType。
    private static readonly string Sql = $$"""
        SELECT 0 AS Kind, l.[DesNo], l.[FlowOver] AS FlowStatus, l.[SendDate] AS SubmittedAt, l.[LeaveSDate] AS StartsAt, l.[LeaveEDate] AS EndsAt,
               l.[LeaveTime] AS Hours, COALESCE(k.[LeaveKind], l.[LeaveKind], '') AS Title, CAST(l.[LeaveDesc] AS nvarchar(max)) AS Reason,
               CAST(l.[AttDesc] AS nvarchar(max)) AS AttachmentNote, l.[LeaveUserId] AS ApplicantId,
               CASE WHEN l.[LeaveKind] = 'G1' THEN CAST(l.[BereaveType] AS varchar(10)) END AS BereaveType, CAST(l.[AttKind] AS nvarchar(max)) AS AttachmentKinds,
               CAST(CASE WHEN ISNULL(l.[DayOfWeek], '') = '' THEN 0 ELSE 1 END AS bit) AS CrossPeriod,
               NULL AS Place, NULL AS Transport, NULL AS StudyMode, NULL AS StudyKind, NULL AS Phone,
               ap.Name AS ApplicantName, ap.Unit AS ApplicantUnit, ap.Title AS ApplicantTitle, ag.Name AS AgentName, ag.Unit AS AgentUnit, ag.Title AS AgentTitle
        FROM [dbo].[Form_Leave] l
        LEFT JOIN [dbo].[Form_LeaveKind] k ON k.[LeaveId] = l.[LeaveKind]
        OUTER APPLY ({{GdPeople.Apply("l.[LeaveUserId]", "l.[LeaveDepNO]")}}) ap
        OUTER APPLY ({{GdPeople.Apply("l.[AgentUserId]", "l.[AgentDepNo]")}}) ag
        WHERE l.[DesNo] = @DesNo AND l.[FlowOver] IN ('{{nameof(GdFormFlowOver.Send)}}', '{{nameof(GdFormFlowOver.Decide)}}')
        UNION ALL
        SELECT 1, t.[DesNo], t.[FlowOver], t.[SendDate], t.[TravelSDate], t.[TravelEDate], t.[TravelTime], COALESCE(t.[TravelMain], ''), NULL,
               CAST(t.[AttDesc] AS nvarchar(max)), t.[ChargeName], NULL, NULL, CAST(0 AS bit),
               t.[Place], CAST(t.[TravelDesc] AS nvarchar(max)), CAST(t.[StudyMode] AS varchar(10)), CAST(t.[StudyKind] AS varchar(10)),
               (SELECT NULLIF(LTRIM(RTRIM(ISNULL(CAST(u.[TelNum] AS varchar(30)), '')
                    + ISNULL('#' + NULLIF(LTRIM(RTRIM(CAST(u.[SubTelNum] AS varchar(20)))), ''), ''))), '')
                FROM [dbo].[Sys_User] u WHERE u.[ID] = t.[ChargeName]),
               ap.Name, ap.Unit, ap.Title, ag.Name, ag.Unit, ag.Title
        FROM [dbo].[Form_Travel] t
        OUTER APPLY ({{GdPeople.Apply("t.[ChargeName]", "t.[ChargeDepName]")}}) ap
        OUTER APPLY ({{GdPeople.Apply("t.[AgentUserId]", "t.[AgentDepNo]")}}) ag
        WHERE t.[DesNo] = @DesNo AND t.[FlowOver] IN ('{{nameof(GdFormFlowOver.Send)}}', '{{nameof(GdFormFlowOver.Decide)}}')
        """;

    private const string SegmentsSql = """
        SELECT g.[LeaveSDate] AS StartsAt, g.[LeaveEDate] AS EndsAt FROM [dbo].[Form_Leave_Group] g
        WHERE g.[DesNo] = @DesNo AND g.[LeaveSDate] IS NOT NULL AND g.[LeaveEDate] IS NOT NULL
        ORDER BY g.[LeaveSDate]
        """;

    /// <summary>GD <c>LeaveFormBean.setSelectBereaveType</c> 的選項，<c>BereaveType</c> 存的是索引。</summary>
    private static readonly string[] Relations = ["父母", "養父母", "繼父母", "配偶", "祖父母", "子女", "配偶之父母", "配偶之養父母", "配偶之繼父母", "曾祖父母", "兄弟姊妹", "配偶之祖父母"];

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/{desNo}", (string desNo, ICurrentUser user, GetGdForm handler, CancellationToken ct) => handler.HandleAsync(user.Id, desNo, ct).ToHttpResultAsync())
        .WithName("GetGdForm");

    public async Task<Result<GdFormDetailDto>> HandleAsync(Guid viewer, string desNo, CancellationToken ct)
    {
        if (!DesNo().IsMatch(desNo)) return AttendanceErrors.GdFormNotFound;
        var scope = await scopes.ForAsync(viewer, FeatureCatalog.AttendanceRecords, ct);
        if (scope.IsEmpty) return AttendanceErrors.GdFormNotFound;

        if (await gd.QueryAsync<FormRow>(Sql, new { DesNo = desNo }, ct) is not { } rows) return AttendanceErrors.GdUnavailable;
        // 單號在 GD 是請假、出差各自的主鍵，字首不同；萬一兩邊都有就不猜。
        if (rows is not [var form] || !GdFlowOver.TryParse(form.FlowStatus, out var flow) || form.StartsAt is not { } starts || form.EndsAt is not { } ends)
            return AttendanceErrors.GdFormNotFound;

        if (await Visible(viewer, scope, form.ApplicantId, ct) is not { } visible) return AttendanceErrors.GdUnavailable;
        if (!visible) return AttendanceErrors.GdFormNotFound;

        IReadOnlyList<GdFormPeriod> segments = [];
        if (form.CrossPeriod)
        {
            if (await gd.QueryAsync<SegmentRow>(SegmentsSql, new { DesNo = desNo }, ct) is not { } groups) return AttendanceErrors.GdUnavailable;
            segments = [.. groups.Select(x => new GdFormPeriod(TaipeiTime.At(x.StartsAt), TaipeiTime.At(x.EndsAt)))];
        }
        var steps = await flows.ReadAsync(desNo, flow == GdFormFlowOver.Send, ct);
        var files = await attachments.ReadAsync(desNo, ct);

        return new GdFormDetailDto(desNo.Trim(), form.Kind, flow, form.Title.Trim(), form.SubmittedAt is { } submitted ? TaipeiTime.At(submitted) : null,
            GdPeople.Person(form.ApplicantName, form.ApplicantUnit, form.ApplicantTitle), GdPeople.Person(form.AgentName, form.AgentUnit, form.AgentTitle),
            TaipeiTime.At(starts), TaipeiTime.At(ends), form.Hours, segments, GdPeople.Text(form.Reason), GdPeople.Text(form.AttachmentNote),
            form.Kind == GdFormKind.Leave ? new GdLeaveDetail(Relation(form.BereaveType), Kinds(form.AttachmentKinds)) : null,
            form.Kind == GdFormKind.Travel ? new GdTravelDetail(GdPeople.Text(form.Place), GdPeople.Text(form.Transport), StudyKind(form.StudyMode, form.StudyKind), form.Phone) : null,
            steps,
            files?.Select(x => new GdFormAttachmentDto(tickets.Issue(x.File, x.FileName, viewer), x.Memo, x.Uploader)).ToList());
    }

    /// <summary>
    /// 看的人的資料範圍是否涵蓋申請人（規則同 <c>AttendanceRoster</c>：own＝自己，unit／unit-tree＝範圍單位目前的成員）；GD 不可用時為 null。
    /// </summary>
    private async Task<bool?> Visible(Guid viewer, EffectiveDataScope scope, string? applicant, CancellationToken ct)
    {
        if (scope.All) return true;
        if (string.IsNullOrWhiteSpace(applicant)) return false;
        var read = await accounts.UsersOfAsync([applicant], ct);
        if (read.Rows is not { } matches) return null;
        if (matches is not [{ UserId: { } user }]) return false;
        if (scope.Own && user == viewer) return true;
        return scope.UnitIds.Count > 0 && await organization.MemberIds(scope.UnitIds).ContainsAsync(user, ct);
    }

    private static string? Relation(string? code) => int.TryParse(code?.Trim(), out var index) && index >= 0 && index < Relations.Length ? Relations[index] : null;

    // GD 把勾選的文件種類以「、」串成一個字串（LeaveForm.js），結尾也有一個。
    private static IReadOnlyList<string> Kinds(string? value)
        => value is null ? [] : [.. value.Split(['、', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()];

    // StudyMode 在 GD 有 bit 與字串兩種寫法（報表以 'true' 比對），兩種都認。
    private static string? StudyKind(string? mode, string? kind)
        => mode?.Trim() is "1" or "true" or "True" ? kind?.Trim() switch { "1" => "教師研習", "2" => "行政人員研習", _ => "研習" } : null;

    // 請假單以 L、出差單以 T 開頭，後接民國年與流水號；只是擋掉明顯不是單號的字串，存在與否由查詢決定。
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]{3,19}$")]
    private static partial Regex DesNo();

    private sealed class FormRow
    {
        public GdFormKind Kind { get; init; }
        public string DesNo { get; init; } = "";
        public string? FlowStatus { get; init; }
        public DateTime? SubmittedAt { get; init; }
        public DateTime? StartsAt { get; init; }
        public DateTime? EndsAt { get; init; }
        public double? Hours { get; init; }
        public string Title { get; init; } = "";
        public string? Reason { get; init; }
        public string? AttachmentNote { get; init; }
        public string? ApplicantId { get; init; }
        public string? BereaveType { get; init; }
        public string? AttachmentKinds { get; init; }
        public bool CrossPeriod { get; init; }
        public string? Place { get; init; }
        public string? Transport { get; init; }
        public string? StudyMode { get; init; }
        public string? StudyKind { get; init; }
        public string? Phone { get; init; }
        public string? ApplicantName { get; init; }
        public string? ApplicantUnit { get; init; }
        public string? ApplicantTitle { get; init; }
        public string? AgentName { get; init; }
        public string? AgentUnit { get; init; }
        public string? AgentTitle { get; init; }
    }

    private sealed class SegmentRow
    {
        public DateTime StartsAt { get; init; }
        public DateTime EndsAt { get; init; }
    }
}
