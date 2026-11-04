using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Organization.Imports;
using AiNexus.Features.Admin.Organization.Members;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Organization;

/// <summary>
/// 組織單位（部門樹）與人員所屬的維護（ADR 0017）。其他模組透過 <see cref="OrganizationDirectory"/> 讀取所屬與下層單位；
/// 管理端點共用 AccessControl 的授權目錄與 <see cref="AdministrativeWrite"/>。
/// 部門也可以從公文系統匯入（<see cref="PreviewUnitImport"/>、<see cref="ApplyUnitImport"/>），來源是 <see cref="GdUnitSource"/>。
/// </summary>
public sealed class OrganizationModule : IFeatureModule
{
    /// <summary>使用者所屬單位：使用者頁要顯示、組織頁要對照，兩個授權任一都可讀。</summary>
    public const string UserUnitsPolicy = Policies.Prefix + "organization-user-units";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddFeaturePolicy(UserUnitsPolicy, FeatureCatalog.Organization, FeatureCatalog.IdentityUsers);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/admin/organization-units").RequireFeature(FeatureCatalog.Organization)
            .MapSlice<ListUnits>()
            .MapSlice<CreateUnit>()
            // 字面路徑 /order、/import、/candidates 與 /{id:guid} 不會互相攔截：後者限定 GUID。
            .MapSlice<ListUnitCandidates>()
            .MapSlice<ReorderUnits>()
            .MapSlice<PreviewUnitImport>()
            .MapSlice<ApplyUnitImport>()
            .MapSlice<UpdateUnit>()
            .MapSlice<DeleteUnit>()
            .MapSlice<ListUnitMembers>()
            .MapSlice<SaveUnitMember>()
            .MapSlice<RemoveUnitMember>();
        api.MapGroup("/admin").RequireAuthorization(UserUnitsPolicy)
            .MapSlice<ListUserUnits>();
    }
}
