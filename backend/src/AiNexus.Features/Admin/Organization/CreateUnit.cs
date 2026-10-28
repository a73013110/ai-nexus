using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>新增單位；<c>ParentId</c> 為 null 代表最上層，<c>HeadRank</c> 有值是上層的首長單位（ADR 0024）。</summary>
public sealed record UnitCreateRequest(Guid? ParentId, string Name, bool Enabled, byte? HeadRank = null);

internal sealed class UnitCreateRequestValidator : RequestValidator<UnitCreateRequest>
{
    public UnitCreateRequestValidator() => RuleFor(x => x.Name).UnitName();
}

/// <summary>新增單位，排到同層最後。Id 由伺服器產生。設成首長單位時取消上層的主管勾選（同一個交易）。會稽核。</summary>
internal sealed class CreateUnit(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", async (UnitCreateRequest body, IRequestActor actor, CreateUnit handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, body, ct)).ToHttpResult())
        .Audited(OrganizationAudit.UnitCreated)
        .WithName("CreateOrganizationUnit");

    public async Task<Result<UnitDto>> HandleAsync(Guid actor, UnitCreateRequest request, CancellationToken ct)
    {
        var unit = new Unit { Id = Guid.NewGuid(), ParentId = request.ParentId, Name = request.Name.Trim(), Enabled = request.Enabled, HeadRank = request.HeadRank };
        var outcome = await audit.MutateAsync(actor, FeatureCatalog.Organization, unit.Id, unit.Id.ToString(), token => UnitRules.SnapshotAsync(db, unit.Id, token), async () =>
        {
            var units = await db.Set<Unit>().ToListAsync(ct);
            unit.SortOrder = UnitRules.NextSortOrder(units, unit.ParentId);
            if (UnitRules.Structure([.. units, unit]) is { } invalid) return invalid;
            db.Add(unit);
            await UnitRules.ClearLedManagersAsync(db, unit, ct);
            return Result.Success;
        }, ct);
        return outcome.IsSuccess ? UnitDto.From(unit, 0) : outcome.Error!;
    }
}
