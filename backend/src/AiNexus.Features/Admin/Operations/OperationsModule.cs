using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Operations;

/// <summary>
/// Operations 是管理端的組合層：使用者列表與洞察、存取目錄、群組儲存與平台用量的回應內嵌 Inference、Billing、Files、Account 的 DTO，
/// 放進 AccessControl 或 Identity 會形成模組循環，所以集中在這個上層模組。端點的授權是呼叫它的頁面（使用者頁、群組頁、平台用量頁），
/// 不是這個模組自己的功能。
/// </summary>
public sealed class OperationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
        => builder.Services.AddFeaturePolicy(FeatureCatalog.OperationsUsage);

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        // 每個端點由呼叫它的頁面授權，所以同一個 /admin 前綴依授權各開一組。
        RouteGroupBuilder Admin(string policy) => api.MapGroup("/admin").RequireAuthorization(policy);
        GetAdminCatalog.Map(Admin(Policies.AccessCatalog));
        var users = Admin(Policies.IdentityUsers);
        ListAdminUsers.Map(users);
        GetAdminUserInsights.Map(users);
        SaveRoleGroup.Map(Admin(Policies.AccessControlGroups));
        GetAdminUsage.Map(Admin(Policies.OperationsUsage));
    }
}
