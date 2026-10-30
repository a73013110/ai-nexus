using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Office.Attendance.Punches;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Imports;

/// <summary>
/// 一次匯入寫入的刷卡，預設依刷卡時間由早到晚；排序、欄位篩選與分頁走共用的 <see cref="TableRequest"/>（ADR 0026），一個每日檔通常一次回傳全部。
/// 刷卡依檔案保存（快照重匯），所以只有這個檔最近一次成功的匯入（<see cref="PunchImportDto.IsCurrent"/>）列得出來，其餘回 <c>attendance_import_not_current</c>。
/// 只列這個檔擁有的刷卡，與 <see cref="PunchImport.PunchCount"/> 一致：已屬於相鄰檔的重複刷卡不在其中。
/// </summary>
internal sealed class ListPunchImportPunches(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/punches", (Guid id, [AsParameters] TableRequest table, ListPunchImportPunches handler, CancellationToken ct)
            => handler.HandleAsync(id, table, ct).ToHttpResultAsync())
        .Audited(AttendanceAudit.PunchRead)
        .WithName("ListAttendancePunchImportPunches");

    public async Task<Result<TablePage<PunchDto>>> HandleAsync(Guid id, TableRequest table, CancellationToken ct)
    {
        var import = await db.Set<PunchImport>().AsNoTracking().Where(x => x.Id == id).ToDtos(db).SingleOrDefaultAsync(ct);
        if (import is null) return AttendanceErrors.ImportNotFound;
        if (!import.IsCurrent) return AttendanceErrors.ImportNotCurrent;
        var punches = db.Set<AttributedPunch>().Where(x => x.PunchFileId == import.PunchFileId);
        return await punches.ToTablePageAsync(table, Columns(), x => x.ToDtos(db), ct);
    }

    /// <summary>欄位 ID 與 <see cref="PunchDto"/> 的屬性同名，前端在本機排序時直接取同名欄位。</summary>
    private TableColumns<AttributedPunch> Columns() => new TableColumns<AttributedPunch>()
        .Range("punchedAt", x => x.PunchedAt)
        .Text("cardNumber", x => x.CardNumber)
        .Text("holder", x => x.UserId != null ? db.Users.Where(u => u.Id == x.UserId).Select(u => u.DisplayName).FirstOrDefault() : x.HolderName)
        .Text("machineCode", x => x.MachineCode)
        .Text("eventCode", x => x.EventCode)
        .Key(x => x.PunchId)
        .DefaultSort("punchedAt");
}
