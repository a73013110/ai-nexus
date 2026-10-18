using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Operations;

/// <summary>
/// Operations 是管理端的組合層：使用者列表與洞察、存取目錄、群組儲存與平台用量的回應內嵌 Inference、Billing、Attachments、Account 的 DTO，
/// 放進 AccessControl 或 Identity 會形成模組循環，所以集中在這個上層模組。授權沿用存取管理（<see cref="Policies.Admin"/>）與平台營運（<see cref="OperationsConfiguration"/>）。
/// </summary>
public sealed class OperationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
        => builder.Services.AddFeaturePolicy(OperationsConfiguration.Policy, OperationsConfiguration.Feature);

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization(Policies.Admin).WithTags("Administration");
        GetAdminCatalog.Map(admin);
        ListAdminUsers.Map(admin);
        GetAdminUserInsights.Map(admin);
        SaveRoleGroup.Map(admin);
        GetAdminUsage.Map(api.MapGroup("/admin").RequireAuthorization(OperationsConfiguration.Policy).WithTags("Administration"));
    }
}
