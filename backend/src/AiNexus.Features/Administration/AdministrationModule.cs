using AiNexus.Features.AccessControl;
using AiNexus.Features.Administration.ModelPolicies;
using AiNexus.Features.Administration.Retrieval;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Administration.Usage;
using AiNexus.Features.Administration.Users;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Administration;

/// <summary>
/// Platform administration. Access management (<see cref="AdministrationConfiguration"/>): roles, groups, features, users,
/// model policies and audited inspection. Platform operations (<see cref="OperationsConfiguration"/>): platform usage and
/// retrieval administration. Each use case has its own file. Every mutation goes through <see cref="AdministrativeAudit"/>.
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

    // Endpoint order is the published OpenAPI order, so the access-management and operations groups interleave.
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
        GetAdminUsage.Map(Operations(root));
        GetEffectiveModelPolicy.Map(root);
    }

    private static RouteGroupBuilder Operations(RouteGroupBuilder root)
        => root.MapGroup("/admin").RequireAuthorization(OperationsConfiguration.Policy).WithTags("Administration");
}
