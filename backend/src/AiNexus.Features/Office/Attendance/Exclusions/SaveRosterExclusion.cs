using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Office.Attendance.Cards;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Exclusions;

/// <summary>不列入出勤的備註（例如「董事」「外派」），讓之後的 HR 知道為什麼。</summary>
public sealed record RosterExclusionRequest(string? Note);

internal sealed class RosterExclusionRequestValidator : RequestValidator<RosterExclusionRequest>
{
    public RosterExclusionRequestValidator() => RuleFor(x => x.Note).OptionalText(RosterExclusion.NoteMaxLength);
}

/// <summary>
/// 把使用者標為不列入出勤，已標過時只更新備註（PUT 冪等）。沒有帳號的資料（只有姓名的持有人、未歸戶卡號）依卡號標，見 <see cref="SaveRosterCardExclusion"/>。
/// 會稽核（<c>admin.attendance_roster_exclusion</c>），快照只記是否不列入；備註是自由文字，不進稽核。
/// </summary>
internal sealed class SaveRosterExclusion(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/{userId:guid}", (Guid userId, RosterExclusionRequest body, ICurrentUser user, SaveRosterExclusion handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, userId, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.AttendanceCardsMaintain)
        .WithName("SaveAttendanceRosterExclusion");

    public Task<Result> HandleAsync(Guid actor, Guid userId, RosterExclusionRequest request, CancellationToken ct)
        => audit.MutateAsync(actor, FeatureCatalog.AttendanceCardsMaintain, "admin.attendance_roster_exclusion", userId, userId.ToString(),
            token => RosterExclusionSnapshot.ReadAsync(db, userId, token), async () =>
            {
                if (!await AssignmentRules.HolderExistsAsync(db, userId, ct)) return AttendanceErrors.UserNotFound;
                var exclusion = await db.Set<RosterExclusion>().SingleOrDefaultAsync(x => x.UserId == userId, ct);
                if (exclusion is null) db.Add(exclusion = new RosterExclusion { UserId = userId });
                exclusion.Note = CardRules.Normalize(request.Note);
                return Result.Success;
            }, ct);
}

/// <summary>新增、修改與移除不列入出勤共用的稽核快照：沒有標記時為 null。</summary>
internal static class RosterExclusionSnapshot
{
    public static async Task<object?> ReadAsync(AppDbContext db, Guid userId, CancellationToken ct)
        => await db.Set<RosterExclusion>().AsNoTracking().AnyAsync(x => x.UserId == userId, ct) ? new { userId, excluded = true } : null;
}
