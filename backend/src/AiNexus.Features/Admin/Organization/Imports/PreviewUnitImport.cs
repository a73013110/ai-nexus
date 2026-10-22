using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Organization.Imports;

/// <summary>
/// 預覽從公文系統匯入（不寫入）：讀 GD 的部門與主管，列出將新增、綁定、更新、停用的單位，主管的變動與略過的列。
/// 只讀，所以測試身分也能看；套用見 <see cref="ApplyUnitImport"/>。
/// </summary>
internal sealed class PreviewUnitImport(AppDbContext db, IUnitSource source)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/import", async (PreviewUnitImport handler, CancellationToken ct) => (await handler.HandleAsync(ct)).ToHttpResult())
        .RequireAuthorization(Policies.OrganizationImport)
        .WithName("PreviewOrganizationImport");

    public async Task<Result<UnitImportPreviewDto>> HandleAsync(CancellationToken ct)
    {
        var read = await source.ReadAsync(ct);
        if (!read.IsSuccess) return read.Error;
        var plan = await UnitImportPlan.CreateAsync(db, read.Value, tracking: false, ct);
        return plan.IsSuccess ? plan.Value.Preview : plan.Error;
    }
}
