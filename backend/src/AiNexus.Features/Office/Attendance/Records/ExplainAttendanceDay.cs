using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>判定說明的對象：某人（使用者或只有姓名的卡號持有人，兩者擇一）的某一個出勤日。</summary>
public sealed class AttendanceExplanationQuery
{
    public DateOnly? Date { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>只有姓名、還沒綁帳號的持有人所依附的卡號（同 <see cref="AttendanceRecordsFilter.Card"/>）。</summary>
    public string? Card { get; set; }
}

/// <summary>
/// 一個人一天的判定說明（<see cref="AttendanceExplanation"/>）：出勤紀錄點開狀態時讀取。
/// 不另查資料：以 <see cref="ListAttendanceRecords"/> 查那一天（含放假日），資料範圍、名單、刷卡、行事曆與表單都與畫面同一份，看不到的人一樣查不到；
/// 再由回應的那一天還原判定的輸入（<see cref="AttendanceExplanation.Explain(AttendancePersonDto, AttendanceRecordDto, WorkdayOptions, DateOnly)"/>）。
/// 同一張卡同一天先後有兩位只有姓名的持有人（當天交接）時，說明的是名單上的第一位。
/// </summary>
internal sealed class ExplainAttendanceDay(ListAttendanceRecords records, IOptions<AttendanceOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/explanation", ([AsParameters] AttendanceExplanationQuery query, ICurrentUser user, ExplainAttendanceDay handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, query, ct))
        .Audited(AttendanceAudit.RecordRead)
        .WithName("ExplainAttendanceDay");

    public async Task<Result<AttendanceExplanationDto>> HandleAsync(Guid viewer, AttendanceExplanationQuery query, CancellationToken ct)
    {
        // 一定要指定一個人：不指定時是整份名單的第一個人，說明的對象會隨名單順序變。
        if (query.Date is not { } date || (query.UserId is null) == (query.Card.TrimToNull() is null)) return AttendanceErrors.RecordsFilterInvalid;

        var filter = new AttendanceRecordsFilter { From = date, To = date, UserId = query.UserId, Card = query.Card, IncludeDaysOff = true };
        var read = await records.HandleAsync(viewer, filter, ListAttendanceRecords.Page.All, ct);
        if (!read.IsSuccess) return read.Error;

        // 範圍外的人、沒持卡的日子與未來的日子都不在名單上：一律當成找不到，不分辨是哪一種。
        var found = read.Value.People.SelectMany(person => person.Days.Where(x => x.Date == date).Select(day => (person, day))).FirstOrDefault();
        if (found is not ({ } person, { } record)) return AttendanceErrors.RecordNotFound;
        return AttendanceExplanation.Explain(person, record, options.Value.Workday, read.Value.Today);
    }
}
