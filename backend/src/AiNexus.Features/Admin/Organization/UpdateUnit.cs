using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>修改單位的名稱、啟用與上層；換上層時排到新一層的最後，同層的順序改用 <see cref="ReorderUnits"/>。</summary>
public sealed record UnitUpdateRequest(Guid? ParentId, string Name, bool Enabled);

internal sealed class UnitUpdateRequestValidator : RequestValidator<UnitUpdateRequest>
{
    public UnitUpdateRequestValidator() => RuleFor(x => x.Name).UnitName();
}

/// <summary>修改或搬移單位；搬移時整棵下層跟著走，成員不變。會稽核。</summary>
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
        unit.Name = request.Name.Trim(); unit.Enabled = request.Enabled;
        // 失敗時 AdministrativeAudit 不會 SaveChanges，並在寫失敗稽核前清空 ChangeTracker，改到一半的實體不會寫入。
        return UnitRules.Structure(units) is { } invalid ? invalid : Result.Success;
    }, ct);
}
