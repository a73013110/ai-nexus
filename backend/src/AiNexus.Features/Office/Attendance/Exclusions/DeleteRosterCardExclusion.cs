using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Exclusions;

/// <summary>恢復列入出勤（依卡號）：移除標記，HR 看全公司時這張卡上沒有帳號的資料再次出現。會稽核（<c>admin.attendance_roster_exclusion_delete</c>）。</summary>
internal sealed class DeleteRosterCardExclusion(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/cards/{cardNumber}", (string cardNumber, ICurrentUser user, DeleteRosterCardExclusion handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, cardNumber, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("DeleteAttendanceRosterCardExclusion");

    public async Task<Result> HandleAsync(Guid actor, string cardNumber, CancellationToken ct)
    {
        if (RosterCardExclusionSnapshot.Normalize(cardNumber) is not { } number) return AttendanceErrors.RosterExclusionNotFound;
        return await audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_roster_exclusion_delete", null, number,
            token => RosterCardExclusionSnapshot.ReadAsync(db, number, token), async () =>
            {
                var exclusion = await db.Set<RosterCardExclusion>().SingleOrDefaultAsync(x => x.CardNumber == number, ct);
                if (exclusion is null) return AttendanceErrors.RosterExclusionNotFound;
                db.Remove(exclusion);
                return Result.Success;
            }, ct);
    }
}
