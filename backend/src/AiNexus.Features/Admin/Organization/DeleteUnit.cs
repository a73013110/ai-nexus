using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>
/// 刪除單位。必須先清空下層與成員，不級聯：業務資料可能已記下這個單位 ID，一次誤刪整段組織的代價高；
/// 不再使用但有歷史的單位改用停用。會稽核。
/// </summary>
internal sealed class DeleteUnit(AppDbContext db, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, IRequestActor actor, DeleteUnit handler, CancellationToken ct) =>
            handler.HandleAsync(actor.Id, id, ct))
        .Audited(OrganizationAudit.UnitDeleted)
        .WithName("DeleteOrganizationUnit");

    public Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct) => write.ExecuteAsync(actor, UnitRules.Target(db, id), async () =>
    {
        var unit = await db.Units.FindAsync([id], ct);
        if (unit is null) return OrganizationErrors.UnitNotFound;
        if (await db.Units.AnyAsync(x => x.ParentId == id, ct) || await db.UnitMembers.AnyAsync(x => x.UnitId == id, ct)) return OrganizationErrors.UnitNotEmpty;
        db.Remove(unit);
        return Result.Success;
    }, ct);
}
