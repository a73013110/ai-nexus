using System.Text.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Features.Platform.Assistant;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Time;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Assistant;

/// <summary>
/// 出勤名單（描述出勤紀錄那一頁）：參數都沒填時是畫面上的條件，與表格、匯出同一份（不分頁）；有填時依參數查，沒給區間就是本月。
/// 資料範圍、名單與判定都走 <see cref="ListAttendanceRecords"/>，看不到的人一樣查不到。
/// </summary>
internal sealed class AttendanceRecordsTool(ListAttendanceRecords records, IOptions<AttendanceOptions> options, IOptions<JsonOptions> json, TimeProvider clock)
    : IAssistantTool
{
    public string Name => "attendance_records";
    public string Title => "查詢出勤紀錄";
    public string Description => "查出勤名單：每人區間內的正常、遲到、早退、未補足、流程中天數，與有缺口的日子。參數都不填＝使用者畫面上的條件。";
    public string Feature => FeatureCatalog.AttendanceRecords;
    public string Guidance => AttendanceAssistant.Guidance;
    public AuditAction? Audit => AttendanceAudit.AssistantRead;
    public bool DescribesPage => true;

    public AssistantParameters Parameters { get; } = new AssistantParameters()
        .Date("from", "區間開始（含），yyyy-MM-dd；沒填是 to 那個月的 1 日或本月 1 日")
        .Date("to", "區間結束（含），yyyy-MM-dd；沒填是 from 那個月的最後一天")
        .Text("person", "只看這個人：帳號、姓名或卡號")
        .Choice("coverage", "只看未補足（unexplained）或流程中（pending）的日子", "unexplained", "pending");

    public async Task<Result<AssistantToolResult>> InvokeAsync(AssistantToolCall call, CancellationToken ct)
    {
        var arguments = call.Arguments;
        var person = arguments.Text("person");
        var filter = arguments.IsEmpty && call.View is { } view ? View(view) : Filter(arguments);
        if (filter is null) return AttendanceErrors.RecordsFilterInvalid;

        var read = await records.HandleAsync(call.Viewer, filter, ListAttendanceRecords.Page.All, ct);
        if (!read.IsSuccess) return read.Error;
        var result = read.Value;
        if (person is not null)
        {
            List<AttendancePersonDto> matched = [.. result.People.Where(x => AttendanceAssistant.Matches(x, person))];
            result = result with { People = matched, Total = matched.Count };
        }
        return AttendanceAssistant.Records(result, options.Value.Workday);
    }

    /// <summary>畫面條件與查詢端點的參數同名、同格式；來自瀏覽器，型別不對時當成條件不合法，其餘交給查詢本身驗證。</summary>
    private AttendanceRecordsFilter? View(JsonElement view)
    {
        try
        {
            return view.Deserialize<AttendanceRecordsFilter>(json.Value.SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private AttendanceRecordsFilter Filter(AssistantArguments arguments)
    {
        var (from, to) = (arguments.Date("from"), arguments.Date("to"));
        var month = from ?? to ?? TaipeiTime.Today(clock);
        from ??= new DateOnly(month.Year, month.Month, 1);
        to ??= new DateOnly(from.Value.Year, from.Value.Month, DateTime.DaysInMonth(from.Value.Year, from.Value.Month));
        GapCoverage? coverage = arguments.Text("coverage") switch
        {
            "unexplained" => GapCoverage.Unexplained,
            "pending" => GapCoverage.Pending,
            _ => null
        };
        return new() { From = from, To = to, Coverage = coverage };
    }
}
