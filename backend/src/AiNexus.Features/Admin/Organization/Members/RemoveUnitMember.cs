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
        .MapDelete("/{id:guid}/members/{userId:guid}", (Guid id, Guid userId, RemoveUnitMember handler, CancellationToken ct) =>
            handler.HandleAsync(id, userId, ct))
        .Audited(OrganizationAudit.MemberDeleted)
        .WithName("RemoveOrganizationUnitMember");

    public async Task<Result> HandleAsync(Guid id, Guid userId, CancellationToken ct)
    {
        // 只動一列：兩人同時移除時後者刪不到，回寫入衝突。
        var begun = await write.BeginAsync(UnitRules.MemberTarget(db, id, userId), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var member = await db.UnitMembers.SingleOrDefaultAsync(x => x.UnitId == id && x.UserId == userId, ct);
        if (member is null) return OrganizationErrors.MemberNotFound;
        db.Remove(member);

        return await transaction.CommitAsync(ct);
    }
}
