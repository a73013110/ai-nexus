using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Exclusions;

/// <summary>恢復列入出勤：移除標記，HR 看全公司時他再次出現。會稽核（<c>admin.attendance_roster_exclusion_delete</c>）。</summary>
internal sealed class DeleteRosterExclusion(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{userId:guid}", (Guid userId, ICurrentUser user, DeleteRosterExclusion handler, CancellationToken ct) => handler.HandleAsync(user.Id, userId, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("DeleteAttendanceRosterExclusion");

    public Task<Result> HandleAsync(Guid actor, Guid userId, CancellationToken ct)
        => audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_roster_exclusion_delete", userId, userId.ToString(),
            token => RosterExclusionSnapshot.ReadAsync(db, userId, token), async () =>
            {
                var exclusion = await db.Set<RosterExclusion>().SingleOrDefaultAsync(x => x.UserId == userId, ct);
                if (exclusion is null) return AttendanceErrors.RosterExclusionNotFound;
                db.Remove(exclusion);
                return Result.Success;
            }, ct);
}
