using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Administration;

public sealed class AdministrationModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<AdministrationOptions>().BindConfiguration("Administration").ValidateOnStart();
        services.AddSingleton<IValidateOptions<AdministrationOptions>, AdministrationOptionsValidator>();
        services.AddScoped<AdminBootstrap>();
        services.AddScoped<AdministrationService>();
        services.AddScoped<AdministrativeAudit>();
        services.AddScoped<AdministrativeReader>();
        services.AddScoped<ModelPolicyService>();
        services.AddSingleton<AdministrativeWriteLock>();
        services.AddFeaturePolicy(AdministrationConfiguration.Policy, AdministrationConfiguration.Feature);
    }

    public static void MapEndpoints(RouteGroupBuilder api) => api.MapAdministration();
}

internal sealed class AdministrationOptionsValidator : IValidateOptions<AdministrationOptions>
{
    public ValidateOptionsResult Validate(string? name, AdministrationOptions x)
        => x.BootstrapAdministrators.Length <= 20 && x.BootstrapAdministrators.All(a => a.Length is > 0 and <= 64 && a.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'))
            ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Invalid bootstrap administrator accounts.");
}
