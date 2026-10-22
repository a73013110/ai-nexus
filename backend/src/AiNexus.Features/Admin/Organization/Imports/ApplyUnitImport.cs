using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization.Imports;

internal sealed class UnitImportRequestValidator : RequestValidator<UnitImportRequest>
{
    public UnitImportRequestValidator() => RuleFor(x => x.Fingerprint).NotEmpty().WithErrorCode("required")
        .MaximumLength(64).WithErrorCode("too_long");
}

/// <summary>
/// 套用從公文系統匯入。不信任預覽的內容：重讀 GD、在行政交易內重讀組織並重算，指紋與預覽不同就回 <c>organization_import_stale</c>，
/// 所以套用的一定是使用者看過的那份變更。GD 在交易外讀取，不讓別人的系統拖住行政寫入鎖；讀不到也照樣寫一筆失敗的稽核。
/// 稽核 <c>admin.organization_import</c> 只記數量（前後的單位、已匯入單位與主管數，以及這次的變動數），完整清單在預覽，會超過稽核的大小上限。
/// </summary>
internal sealed class ApplyUnitImport(AppDbContext db, IUnitSource source, AdministrativeAudit audit)
{
    public const string AuditAction = "admin.organization_import";

    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/import", async (UnitImportRequest body, IRequestActor actor, ApplyUnitImport handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, body, ct)).ToHttpResult())
        .RequireAuthorization(Policies.OrganizationImport)
        .WithName("ApplyOrganizationImport");

    public async Task<Result<UnitImportResultDto>> HandleAsync(Guid actor, UnitImportRequest request, CancellationToken ct)
    {
        var read = await source.ReadAsync(ct);
        UnitImportResultDto? result = null;
        var outcome = await audit.MutateAsync(actor, FeatureCatalog.OrganizationImport, AuditAction, null, "gd", SnapshotAsync, async () =>
        {
            if (!read.IsSuccess) return read.Error;
            var plan = await UnitImportPlan.CreateAsync(db, read.Value, tracking: true, ct);
            if (!plan.IsSuccess) return plan.Error;
            if (!string.Equals(plan.Value.Fingerprint, request.Fingerprint, StringComparison.OrdinalIgnoreCase)) return OrganizationErrors.ImportStale;
            result = await plan.Value.ApplyAsync(db, ct);
            return Result.Success;
        }, ct);
        return outcome.IsSuccess ? result! : outcome.Error;

        // 變更前後各一次；變動數只在套用後才有，變更前為 null。
        async Task<object?> SnapshotAsync(CancellationToken token) => new
        {
            units = await db.Set<Unit>().CountAsync(token),
            gdUnits = await db.Set<Unit>().CountAsync(x => x.GdDepNo != null, token),
            managers = await db.Set<UnitMember>().CountAsync(x => x.IsManager, token),
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
