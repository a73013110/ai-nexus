using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>
/// 修改單位的名稱、啟用、上層與首長單位層級（null＝一般單位，ADR 0024）；換上層時排到新一層的最後，同層的順序改用 <see cref="ReorderUnits"/>。
/// </summary>
public sealed record UnitUpdateRequest(Guid? ParentId, string Name, bool Enabled, byte? HeadRank, long RowVersion);

internal sealed class UnitUpdateRequestValidator : RequestValidator<UnitUpdateRequest>
{
    public UnitUpdateRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(Unit.NameMaxLength);
        RuleFor(x => x.RowVersion).GreaterThan(0).WithErrorCode(RuleCode.Required);
    }
}

/// <summary>修改或搬移單位；搬移時整棵下層跟著走，成員不變。設成首長單位時取消上層的主管勾選（同一個交易）。會稽核。</summary>
internal sealed class UpdateUnit(AppDbContext db, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", (Guid id, UnitUpdateRequest body, UpdateUnit handler, CancellationToken ct) =>
            handler.HandleAsync(id, body, ct))
        .Audited(OrganizationAudit.UnitUpdated)
        .WithName("UpdateOrganizationUnit");

    public async Task<Result> HandleAsync(Guid id, UnitUpdateRequest request, CancellationToken ct)
    {
        var begun = await write.BeginAsync(UnitRules.Target(db, id), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var units = await db.Units.ToListAsync(ct);
        var unit = units.SingleOrDefault(x => x.Id == id);
        if (unit is null) return OrganizationErrors.UnitNotFound;
        db.ExpectRowVersion(unit, request.RowVersion);
        if (unit.ParentId != request.ParentId)
        {
            unit.SortOrder = UnitRules.NextSortOrder(units.Where(x => x.Id != id), request.ParentId);
            unit.ParentId = request.ParentId;
        }
        unit.Name = request.Name.Trim(); unit.Enabled = request.Enabled; unit.HeadRank = request.HeadRank;
        // 沒有提交就離開會回滾並清空 ChangeTracker，改到一半的實體不會寫入。
        if (UnitRules.Structure(units) is { } invalid) return invalid;
        await UnitRules.ClearLedManagersAsync(db, unit, ct);

        return await transaction.CommitAsync(ct);
    }
}
