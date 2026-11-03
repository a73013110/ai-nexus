using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization.Imports;

internal sealed class UnitImportRequestValidator : RequestValidator<UnitImportRequest>
{
    public UnitImportRequestValidator()
    {
        RuleFor(x => x.Fingerprint).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Excluded).Must(x => x is null || x.Count <= MaxExcluded).WithErrorCode(RuleCode.TooMany);
    }

    /// <summary>GD 的部門以百計；上限只為擋住異常大的請求。</summary>
    public const int MaxExcluded = 5000;
}

/// <summary>
/// 套用從公文系統匯入。不信任預覽的內容：重讀 GD、在行政交易內重讀組織並重算，指紋與預覽不同就回 <c>organization_import_stale</c>，
/// 所以套用的一定是使用者看過的那份變更；管理員取消勾選的部門（<c>Excluded</c>）再從同一份輸入排除後套用。GD 在交易外讀取，不讓別人的系統拖住行政寫入鎖；讀不到時請求稽核照樣記下錯誤代碼。
/// 稽核 <c>organization.unit.imported</c> 只記數量（前後的單位、已匯入單位與主管數，以及這次的變動數），完整清單在預覽，會超過稽核的大小上限。
/// </summary>
internal sealed class ApplyUnitImport(AppDbContext db, GdUnitSource source, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/import", (UnitImportRequest body, ApplyUnitImport handler, CancellationToken ct) =>
            handler.HandleAsync(body, ct))
        .RequireFeature(FeatureCatalog.OrganizationImport)
        .Audited(OrganizationAudit.UnitImported)
        .WithName("ApplyOrganizationImport");

    public async Task<Result<UnitImportResultDto>> HandleAsync(UnitImportRequest request, CancellationToken ct)
    {
        var read = await source.ReadAsync(ct);
        if (!read.IsSuccess) return read.Error;
        UnitImportResultDto? result = null;
        var begun = await write.BeginAsync(AuditTarget.Of("gd", SnapshotAsync).Locking(OrganizationLocks.Tree), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var plan = await UnitImportPlan.CreateAsync(db, read.Value, tracking: true, ct);
        if (!plan.IsSuccess) return plan.Error;
        if (!string.Equals(plan.Value.Fingerprint, request.Fingerprint, StringComparison.OrdinalIgnoreCase)) return OrganizationErrors.ImportStale;
        var chosen = plan.Value.Excluding(request.Excluded?.ToHashSet() ?? []);
        if (!chosen.IsSuccess) return chosen.Error;
        result = await chosen.Value.ApplyAsync(db, ct);

        return await transaction.CommitAsync(() => result, ct);

        // 變更前後各一次；變動數只在套用後才有，變更前為 null。
        async Task<object?> SnapshotAsync(CancellationToken token) => new
        {
            units = await db.Units.CountAsync(token),
            gdUnits = await db.Units.CountAsync(x => x.GdDepNo != null, token),
            managers = await db.UnitMembers.CountAsync(x => x.IsManager, token),
            excluded = request.Excluded?.Count ?? 0,
            created = result?.Created,
            adopted = result?.Adopted,
            updated = result?.Updated,
            disabled = result?.Disabled,
            managersAdded = result?.ManagersAdded,
            managersRemoved = result?.ManagersRemoved,
            issues = result?.Issues,
        };
    }
}
