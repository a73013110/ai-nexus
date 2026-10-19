using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>一層單位的新順序：<c>UnitIds</c> 必須剛好是 <c>ParentId</c> 底下現有的全部單位（null＝最上層）。</summary>
public sealed record UnitOrderRequest(Guid? ParentId, IReadOnlyList<Guid> UnitIds);

internal sealed class UnitOrderRequestValidator : RequestValidator<UnitOrderRequest>
{
    public UnitOrderRequestValidator() => RuleFor(x => x.UnitIds).NotEmpty()
        .Must(x => x.Count <= UnitRules.MaxSiblings).WithErrorCode("too_many")
        .Must(x => x.Distinct().Count() == x.Count).WithErrorCode("duplicate");
}

/// <summary>
/// 重排同一層的單位，依清單順序重新編號。要求送來完整的一層：畫面過期（期間有人新增或搬走單位）時拒絕，
/// 不會把別人剛加的單位排到意料之外的位置。會稽核。
/// </summary>
internal sealed class ReorderUnits(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/order", async (UnitOrderRequest body, IRequestActor actor, ReorderUnits handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, body, ct)).ToHttpResult())
        .WithName("ReorderOrganizationUnits");

    public Task<Result> HandleAsync(Guid actor, UnitOrderRequest request, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.Organization, "admin.organization_order", request.ParentId, request.ParentId?.ToString() ?? "root", token => SnapshotAsync(request.ParentId, token), async () =>
    {
        var siblings = await db.Set<Unit>().Where(x => x.ParentId == request.ParentId).ToListAsync(ct);
        if (siblings.Count != request.UnitIds.Count || siblings.Any(x => !request.UnitIds.Contains(x.Id))) return OrganizationErrors.OrderStale;
        for (var i = 0; i < request.UnitIds.Count; i++) siblings.Single(x => x.Id == request.UnitIds[i]).SortOrder = (i + 1) * UnitRules.SortStep;
        return Result.Success;
    }, ct);

    private async Task<object?> SnapshotAsync(Guid? parentId, CancellationToken ct) => new
    {
        parentId,
        unitIds = await db.Set<Unit>().AsNoTracking().Where(x => x.ParentId == parentId).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => x.Id).ToArrayAsync(ct),
    };
}
