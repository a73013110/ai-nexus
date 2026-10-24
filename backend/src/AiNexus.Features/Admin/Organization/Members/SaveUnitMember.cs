using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization.Members;

/// <summary>成員在該單位的身分；設為主要所屬時，會取消該使用者在其他單位的主要所屬。</summary>
[ValidatedInHandler("Only two booleans; whether the unit and user exist is checked inside the administrative transaction so rejections are audited.")]
public sealed record UnitMemberRequest(bool IsPrimary, bool IsManager);

/// <summary>
/// 加入或更新成員。只能加入未刪除的使用者，停用的單位不能再加入新成員（既有成員仍可修改）。
/// 主要所屬在同一個交易內轉移；先清掉舊的再寫新的，否則一人一筆的 filtered unique index 會在中途擋下。會稽核。
/// </summary>
internal sealed class SaveUnitMember(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, UnitMemberRequest body, IRequestActor actor, SaveUnitMember handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, id, userId, body, ct)).ToHttpResult())
        .WithName("SaveOrganizationUnitMember");

    public Task<Result> HandleAsync(Guid actor, Guid id, Guid userId, UnitMemberRequest request, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.Organization, "admin.organization_member", id, userId.ToString(), token => UnitRules.MembershipsAsync(db, userId, token), async () =>
    {
        var unit = await db.Set<Unit>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (unit is null) return OrganizationErrors.UnitNotFound;
        var member = await db.Set<UnitMember>().SingleOrDefaultAsync(x => x.UnitId == id && x.UserId == userId, ct);
        if (member is null)
        {
            if (!await db.Users.AnyAsync(x => x.Id == userId && x.DeletedAt == null, ct)) return OrganizationErrors.UserNotFound;
            if (!unit.Enabled) return OrganizationErrors.UnitDisabled;
            member = new UnitMember { UnitId = id, UserId = userId };
            db.Add(member);
        }
        if (request.IsPrimary && !member.IsPrimary)
            await db.Set<UnitMember>().Where(x => x.UserId == userId && x.UnitId != id && x.IsPrimary).ExecuteUpdateAsync(x => x.SetProperty(y => y.IsPrimary, false), ct);
        member.IsPrimary = request.IsPrimary; member.IsManager = request.IsManager;
        return Result.Success;
    }, ct);
}
