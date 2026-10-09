using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Administration;

/// <summary>
/// Platform administration: roles, groups, features, users, model policies, usage, audited inspection and retrieval
/// operations. Each use case has its own file. Every mutation goes through <see cref="AdministrativeAudit"/>.
/// </summary>
public sealed class AdministrationModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<AdministrationOptions>().BindConfiguration("Administration").ValidateOnStart();
        services.AddSingleton<IValidateOptions<AdministrationOptions>, AdministrationOptionsValidator>();
        services.AddScoped<ISignInGrant, AdminBootstrap>();
        services.AddScoped<AdministrativeAudit>();
        services.AddScoped<AdministrativeReadAudit>();
        services.AddSingleton<AdministrativeWriteLock>();
        services.AddScoped<GetAdminCatalog>();
        services.AddScoped<ListAdminUsers>();
        services.AddScoped<GetAdminUserInsights>();
        services.AddScoped<ListAdminUserConversations>();
        services.AddScoped<ReadAdminConversation>();
        services.AddScoped<UserAccountAdministration>();
        services.AddScoped<SetUserRoles>();
        services.AddScoped<SetUserAttachmentStorage>();
        services.AddScoped<GetUserModelPolicy>();
        services.AddScoped<SetUserModelPolicy>();
        services.AddScoped<PreviewUserAccess>();
        services.AddScoped<SaveRole>();
        services.AddScoped<SaveRoleGroup>();
        services.AddScoped<SaveFeature>();
        services.AddScoped<GetAdminUsage>();
        services.AddScoped<ManageEmbeddingProfiles>();
        services.AddFeaturePolicy(AdministrationConfiguration.Policy, AdministrationConfiguration.Feature);
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder root)
    {
        var api = root.MapGroup("/admin").RequireAuthorization(AdministrationConfiguration.Policy).WithTags("Administration");
        var knowledge = api.MapGroup("/knowledge");
        ManageEmbeddingProfiles.MapList(knowledge);
        ManageEmbeddingProfiles.MapRebuild(knowledge);
        ManageEmbeddingProfiles.MapActivate(knowledge);
        ManageEmbeddingProfiles.MapClear(knowledge);
        ReadRetrievalCapabilities.MapGet(knowledge);
        ReadRetrievalCapabilities.MapProbe(knowledge);
        TestAdminRetrieval.Map(knowledge);
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
        GetAdminUsage.Map(api);
        GetEffectiveModelPolicy.Map(root);
    }
}

internal sealed class AdministrationOptionsValidator : IValidateOptions<AdministrationOptions>
{
    public ValidateOptionsResult Validate(string? name, AdministrationOptions x)
        => x.BootstrapAdministrators.Length <= 20 && x.BootstrapAdministrators.All(a => a.Length is > 0 and <= 64 && a.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'))
            ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Invalid bootstrap administrator accounts.");
}
