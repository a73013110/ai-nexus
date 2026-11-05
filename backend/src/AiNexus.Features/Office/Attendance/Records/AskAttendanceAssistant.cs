using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Assistant;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤紀錄的頁內助理：查詢條件與表格相同（query string，含補足篩選），body 是提問。
/// 以 <see cref="ListAttendanceRecords"/> 取整份名單（不分頁），資料範圍、名單與判定都與畫面同一份，再交給 <see cref="AssistantResponder"/> 回答；
/// 稽核記查詢的區間與人數（與查詢出勤紀錄相同），不記提問與回答。
/// </summary>
internal sealed class AskAttendanceAssistant(ListAttendanceRecords records, AssistantResponder assistant, IOptions<AttendanceOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/assistant", ([AsParameters] AttendanceRecordsFilter filter, AssistantQuestion question, HttpContext http, ICurrentUser user,
                AskAttendanceAssistant handler, CancellationToken ct)
            => handler.HandleAsync(http, user.Id, filter, question, ct).ToStreamResultAsync(http))
        .RequireRateLimiting(AssistantModule.RateLimit).WithRequestBodyLimit(AssistantModule.BodyLimit)
        .Audited(AttendanceAudit.AssistantRead)
        .WithName("AskAttendanceAssistant").Produces<AssistantEventDto>(200, "text/event-stream");

    public async Task<Result> HandleAsync(HttpContext http, Guid viewer, AttendanceRecordsFilter filter, AssistantQuestion question, CancellationToken ct)
    {
        var read = await records.HandleAsync(viewer, filter, ListAttendanceRecords.Page.All, ct);
        if (!read.IsSuccess) return read.Error;
        return await assistant.AnswerAsync(http, viewer, question, AttendanceAssistant.Ground(read.Value, options.Value.Workday), ct);
    }
}
