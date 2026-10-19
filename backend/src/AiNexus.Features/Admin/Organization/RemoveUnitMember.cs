using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>把使用者移出單位。移除主要所屬後不會自動指定另一個單位，由管理員決定。會稽核。</summary>
internal sealed class RemoveUnitMember(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, IRequestActor actor, RemoveUnitMember handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, id, userId, ct)).ToHttpResult())
        .WithName("RemoveOrganizationUnitMember");

    public Task<Result> HandleAsync(Guid actor, Guid id, Guid userId, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.Organization, "admin.organization_member_delete", id, userId.ToString(), token => UnitRules.MembershipsAsync(db, userId, token), async () =>
    {
        var member = await db.Set<UnitMember>().SingleOrDefaultAsync(x => x.UnitId == id && x.UserId == userId, ct);
        if (member is null) return OrganizationErrors.MemberNotFound;
        db.Remove(member);
        return Result.Success;
    }, ct);
}
