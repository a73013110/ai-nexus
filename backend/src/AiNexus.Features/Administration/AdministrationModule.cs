using AiNexus.Features.AccessControl;
using AiNexus.Features.Administration.ModelPolicies;
using AiNexus.Features.Administration.Navigation;
using AiNexus.Features.Administration.Retrieval;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Administration.Usage;
using AiNexus.Features.Administration.Users;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Administration;

/// <summary>
/// 平台管理模組。存取管理（<see cref="AdministrationConfiguration"/>）包含角色、群組、功能、選單群組、使用者、模型策略與稽核式檢視；
/// 平台營運（<see cref="OperationsConfiguration"/>）包含平台用量與檢索管理。每個 use case 各自一個檔案；所有變更都必須經過 <see cref="AdministrativeAudit"/>。
/// </summary>
public sealed class AdministrationModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSettings<AdministrationOptions, AdministrationOptionsValidator>(AdministrationOptions.Section);
        services.AddScoped<ISignInGrant, AdminBootstrap>();
        services.AddScoped<AdministrativeAudit>();
        services.AddScoped<AdministrativeReadAudit>();
        services.AddSingleton<AdministrativeWriteLock>();
        services.AddFeaturePolicy(AdministrationConfiguration.Policy, AdministrationConfiguration.Feature);
        services.AddFeaturePolicy(OperationsConfiguration.Policy, OperationsConfiguration.Feature);
    }

    // 端點註冊順序即 OpenAPI 發布順序，所以「存取管理」與「平台營運」兩組端點會交錯排列，不可隨意重排。
    public static void MapEndpoints(RouteGroupBuilder root)
    {
        var knowledge = Operations(root).MapGroup("/knowledge");
        ManageEmbeddingProfiles.MapList(knowledge);
        ManageEmbeddingProfiles.MapRebuild(knowledge);
        ManageEmbeddingProfiles.MapActivate(knowledge);
        ManageEmbeddingProfiles.MapClear(knowledge);
        ReadRetrievalCapabilities.MapGet(knowledge);
        ReadRetrievalCapabilities.MapProbe(knowledge);
        TestAdminRetrieval.Map(knowledge);
        var api = root.MapGroup("/admin").RequireAuthorization(AdministrationConfiguration.Policy).WithTags("Administration");
        GetAdminCatalog.Map(api);
        ListAdminUsers.Map(api);
        ManageUserAccounts.MapCreate(api);
        ManageUserAccounts.MapUpdate(api);
        ManageUserAccounts.MapDelete(api);
        GetAdminUserInsights.Map(api);
        ListAdminUserConversations.Map(api);
        ReadAdminConversation.Map(api);
        SetUserRoles.Map(api);
        SetUserAttachmentStorage.Map(api);
        GetUserModelPolicy.Map(api);
        SetUserModelPolicy.Map(api);
        PreviewUserAccess.Map(api);
        SaveRole.Map(api);
        SaveRoleGroup.Map(api);
        SaveFeature.Map(api);
        CreateNavigationGroup.Map(api);
        UpdateNavigationGroup.Map(api);
        DeleteNavigationGroup.Map(api);
        GetAdminUsage.Map(Operations(root));
        GetEffectiveModelPolicy.Map(root);
    }

    private static RouteGroupBuilder Operations(RouteGroupBuilder root)
        => root.MapGroup("/admin").RequireAuthorization(OperationsConfiguration.Policy).WithTags("Administration");
}
