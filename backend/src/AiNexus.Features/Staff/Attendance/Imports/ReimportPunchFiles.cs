using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>
/// 要重匯的每日檔的檔名日期區間，含頭尾；起訖相同就是單一檔案。
/// 檔名日期是 <c>yyyyMMdd.txt</c> 的日期，不是出勤日：檔案可能含前一天深夜的刷卡。
/// </summary>
public sealed record PunchReimportRequest(DateOnly From, DateOnly To);

/// <summary>
/// 重匯的結果：找到的檔各一筆匯入紀錄（成功或失敗），依檔名日期由舊到新；<see cref="MissingFiles"/> 是來源資料夾裡沒有的檔名，沒有匯入紀錄。
/// </summary>
public sealed record PunchReimportResult(IReadOnlyList<PunchImportDto> Imports, IReadOnlyList<string> MissingFiles);

internal sealed class PunchReimportRequestValidator : RequestValidator<PunchReimportRequest>
{
    public PunchReimportRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.From).Must(PunchFileParser.IsSupported).WithErrorCode("range");
        RuleFor(x => x.To).Must(PunchFileParser.IsSupported).WithErrorCode("range")
            .GreaterThanOrEqualTo(x => x.From).WithErrorCode("order")
            // 台灣的今天，不是伺服器的今天；今天的檔可能已經輸出，所以包含今天。
            .Must(x => x <= TaipeiTime.Today(clock)).WithErrorCode("future")
            .Must((request, to) => to.DayNumber - request.From.DayNumber < ReimportPunchFiles.MaxDays).WithErrorCode("too_long");
    }
}

/// <summary>
/// HR 手動重匯一段日期的每日檔：不論檔案有沒有變動都重匯，匯入紀錄記為 <c>manual</c> 並帶操作者。操作本身就是匯入紀錄，不另寫稽核。
/// <para>
/// 依檔名日期由舊到新逐檔交給 <see cref="PunchImporter"/>，與排程掃描同一個順序，所以相鄰兩天重複的刷卡一樣歸先寫入的檔。
/// 每個檔各自取得 <see cref="PunchImportGate"/>、各自一個交易：排程可以插在兩個檔之間，不會被整段重匯擋住；中途失敗或請求中斷時，已完成的檔照常保留。
/// </para>
/// <para>
/// 在請求內同步處理，不走背景工作：每日檔每張卡只有最早與最晚兩筆，單檔通常不到一秒，區間上限 <see cref="MaxDays"/> 天也在數十秒內，
/// IIS in-process 沒有請求逾時；HR 要在對話框立刻看到每個檔的結果。改走 durable job 要多一套佇列、進度與結果查詢，換來的只是不必等。
/// </para>
/// 找不到的檔列在結果的 <see cref="PunchReimportResult.MissingFiles"/>；整段都找不到才是錯誤（<c>attendance_import_file_not_found</c>，不留紀錄），
/// 單一檔案的重匯因此與以前一樣。來源資料夾讀不到（<c>attendance_import_source_unavailable</c>）就停止，之後的檔都不會成功。
/// 檔案存在但匯入失敗（例如格式錯誤）不是請求失敗：結果裡是狀態為 <c>failed</c> 的紀錄，Punches 維持上次成功的內容。
/// </summary>
internal sealed class ReimportPunchFiles(AppDbContext db, PunchImporter importer)
{
    /// <summary>一次最多幾天（幾個檔）：一個月，與出勤紀錄查詢一致，也限制同步請求的等待時間。</summary>
    public const int MaxDays = 31;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (PunchReimportRequest body, ICurrentUser user, ReimportPunchFiles handler, CancellationToken ct) => handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceImportsReimport)
        .WithName("ReimportAttendancePunchFiles");

    public async Task<Result<PunchReimportResult>> HandleAsync(Guid actor, PunchReimportRequest request, CancellationToken ct)
    {
        var imported = new List<Guid>();
        var missing = new List<string>();
        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            var fileName = PunchFileParser.FileNameFor(date);
            var result = await importer.ReimportAsync(fileName, actor, ct);
            if (result.IsSuccess) imported.Add(result.Value.Id);
            else if (result.Error == AttendanceErrors.ImportFileNotFound) missing.Add(fileName);
            else return result.Error;
        }
        if (imported.Count == 0) return AttendanceErrors.ImportFileNotFound;
        var imports = await db.Set<PunchImport>().AsNoTracking().Where(x => imported.Contains(x.Id)).ToDtos(db).ToListAsync(ct);
        return new PunchReimportResult([.. imports.OrderBy(x => x.FileName, StringComparer.Ordinal)], missing);
    }
}
