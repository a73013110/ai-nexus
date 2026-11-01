using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization.Members;

/// <summary>把使用者移出單位。移除主要所屬後不會自動指定另一個單位，由管理員決定。會稽核。</summary>
internal sealed class RemoveUnitMember(AppDbContext db, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}/members/{userId:guid}", (Guid id, Guid userId, IRequestActor actor, RemoveUnitMember handler, CancellationToken ct) =>
            handler.HandleAsync(actor.Id, id, userId, ct))
        .Audited(OrganizationAudit.MemberDeleted)
        .WithName("RemoveOrganizationUnitMember");

    public Task<Result> HandleAsync(Guid actor, Guid id, Guid userId, CancellationToken ct) => write.ExecuteAsync(actor, UnitRules.MemberTarget(db, id, userId), async () =>
    {
        var member = await db.UnitMembers.SingleOrDefaultAsync(x => x.UnitId == id && x.UserId == userId, ct);
        if (member is null) return OrganizationErrors.MemberNotFound;
        db.Remove(member);
        return Result.Success;
    }, ct);
}
