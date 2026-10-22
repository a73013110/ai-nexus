using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>指定要重匯的每日檔日期（檔名 <c>yyyyMMdd.txt</c> 的日期，不是出勤日；檔案可能含前一天深夜的刷卡）。</summary>
public sealed record PunchReimportRequest(DateOnly Date);

internal sealed class PunchReimportRequestValidator : RequestValidator<PunchReimportRequest>
{
    public PunchReimportRequestValidator()
    {
        RuleFor(x => x.Date).Must(PunchFileParser.IsSupported).WithErrorCode("range");
    }
}

/// <summary>
/// HR 手動重匯某一天的檔案：不論檔案有沒有變動都重匯，匯入紀錄記為 <c>manual</c> 並帶操作者。
/// 找不到檔案或來源資料夾讀不到回錯誤（<c>attendance_import_file_not_found</c>、<c>attendance_import_source_unavailable</c>），不留紀錄；
/// 檔案存在但匯入失敗（例如格式錯誤）不是請求失敗：回傳狀態為 <c>failed</c> 的匯入紀錄，Punches 維持上次成功的內容。
/// 與排程匯入共用互斥鎖，撞在一起時等前一個結束。操作本身就是匯入紀錄，不另寫稽核。
/// </summary>
internal sealed class ReimportPunchFile(AppDbContext db, PunchImporter importer)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (PunchReimportRequest body, ICurrentUser user, ReimportPunchFile handler, CancellationToken ct) => handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceImportsReimport)
        .WithName("ReimportAttendancePunchFile");

    public async Task<Result<PunchImportDto>> HandleAsync(Guid actor, PunchReimportRequest request, CancellationToken ct)
    {
        var result = await importer.ReimportAsync(PunchFileParser.FileNameFor(request.Date), actor, ct);
        if (!result.IsSuccess) return result.Error;
        return await db.Set<PunchImport>().AsNoTracking().Where(x => x.Id == result.Value.Id).ToDtos(db).SingleAsync(ct);
    }
}
