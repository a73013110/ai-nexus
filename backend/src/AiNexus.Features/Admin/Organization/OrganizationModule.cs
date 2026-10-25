using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Organization.Imports;
using AiNexus.Features.Admin.Organization.Members;
using AiNexus.Platform.Accounts;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Organization;

/// <summary>
/// 組織單位（部門樹）與人員所屬的維護（ADR 0017）。其他模組透過 <see cref="OrganizationDirectory"/> 讀取所屬與下層單位；
/// 管理端點共用 AccessControl 的授權目錄與 <see cref="AdministrativeAudit"/>。
/// 部門也可以從公文系統匯入（<see cref="PreviewUnitImport"/>、<see cref="ApplyUnitImport"/>），來源是 <see cref="GdUnitSource"/>。
/// </summary>
public sealed class OrganizationModule : IFeatureModule
{
    /// <summary>使用者所屬單位：使用者頁要顯示、組織頁要對照，兩個授權任一都可讀。</summary>
    public const string UserUnitsPolicy = Policies.Prefix + "organization-user-units";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<OrganizationDirectory>();
        builder.Services.AddScoped<IUnitMembership>(sp => sp.GetRequiredService<OrganizationDirectory>());
        builder.Services.AddAccountReviewSource<ManagerAccountReview>();
        builder.Services.AddScoped<GdUnitSource>();
        builder.Services.AddFeaturePolicy(FeatureCatalog.Organization);
        builder.Services.AddFeaturePolicy(FeatureCatalog.OrganizationImport);
        builder.Services.AddFeaturePolicy(UserUnitsPolicy, FeatureCatalog.Organization, FeatureCatalog.IdentityUsers);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var units = api.MapGroup("/admin/organization-units").RequireAuthorization(Policies.Organization);
        ListUnits.Map(units);
        CreateUnit.Map(units);
        // 字面路徑 /order、/import、/candidates 與 /{id:guid} 不會互相攔截：後者限定 GUID。
        ListUnitCandidates.Map(units);
        ReorderUnits.Map(units);
        PreviewUnitImport.Map(units);
        ApplyUnitImport.Map(units);
        UpdateUnit.Map(units);
        DeleteUnit.Map(units);
        ListUnitMembers.Map(units);
        SaveUnitMember.Map(units);
        RemoveUnitMember.Map(units);
        ListUserUnits.Map(api.MapGroup("/admin").RequireAuthorization(UserUnitsPolicy));
    }
}
