using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>
/// 修改單位的名稱、啟用、上層與首長單位層級（null＝一般單位，ADR 0024）；換上層時排到新一層的最後，同層的順序改用 <see cref="ReorderUnits"/>。
/// </summary>
public sealed record UnitUpdateRequest(Guid? ParentId, string Name, bool Enabled, byte? HeadRank = null);

internal sealed class UnitUpdateRequestValidator : RequestValidator<UnitUpdateRequest>
{
    public UnitUpdateRequestValidator() => RuleFor(x => x.Name).UnitName();
}

/// <summary>修改或搬移單位；搬移時整棵下層跟著走，成員不變。設成首長單位時取消上層的主管勾選（同一個交易）。會稽核。</summary>
internal sealed class UpdateUnit(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", async (Guid id, UnitUpdateRequest body, IRequestActor actor, UpdateUnit handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, id, body, ct)).ToHttpResult())
        .Audited(OrganizationAudit.UnitUpdated)
        .WithName("UpdateOrganizationUnit");

    public Task<Result> HandleAsync(Guid actor, Guid id, UnitUpdateRequest request, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.Organization, id, id.ToString(), token => UnitRules.SnapshotAsync(db, id, token), async () =>
    {
        var units = await db.Set<Unit>().ToListAsync(ct);
        var unit = units.SingleOrDefault(x => x.Id == id);
        if (unit is null) return OrganizationErrors.UnitNotFound;
        if (unit.ParentId != request.ParentId)
        {
            unit.SortOrder = UnitRules.NextSortOrder(units.Where(x => x.Id != id), request.ParentId);
            unit.ParentId = request.ParentId;
        }
        unit.Name = request.Name.Trim(); unit.Enabled = request.Enabled; unit.HeadRank = request.HeadRank;
        // 失敗時 AdministrativeAudit 不會 SaveChanges，並在寫失敗稽核前清空 ChangeTracker，改到一半的實體不會寫入。
        if (UnitRules.Structure(units) is { } invalid) return invalid;
        await UnitRules.ClearLedManagersAsync(db, unit, ct);
        return Result.Success;
    }, ct);
}
