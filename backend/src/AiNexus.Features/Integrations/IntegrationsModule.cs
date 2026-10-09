using AiNexus.Features.AccessControl;
using AiNexus.Features.Configuration;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Integrations;

public sealed class IntegrationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSqlDatabase<LegacyGdwebDatabase>();
        services.AddSqlDatabase<LegacyMeihoDatabase>();
        services.AddOptions<IntegrationsOptions>().Configure<IConfiguration>((o, c) => NexusSettings.Integrations(c, o)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<IntegrationsOptions>, IntegrationsOptionsValidator>();
        services.AddScoped<SourceGateway>();
        services.AddScoped<ImportSourceRecord>();
        services.AddScoped<StartSourceChat>();
        services.AddScoped<IControlledSourceAdapter, GdwebSource>();
        services.AddScoped<IControlledSourceAdapter, MeihoSource>();
        services.AddFeaturePolicy(FeatureIds.Integrations);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/integrations").RequireAuthorization(Policies.Integrations).WithTags("Integrations");
        BrowseSources.Map(routes);
        ImportSourceRecord.Map(routes);
        StartSourceChat.Map(routes);
    }
}

internal sealed class IntegrationsOptionsValidator : IValidateOptions<IntegrationsOptions>
{
    public ValidateOptionsResult Validate(string? name, IntegrationsOptions x)
        => new[] { x.Gdweb, x.Meiho }.All(s => s.CommandTimeoutSeconds is >= 2 and <= 30 && s.MaxResults is >= 1 and <= 50 && s.AllowedGroupIds.Length <= 20
               && s.AllowedGroupIds.All(g => g.Length is >= 1 and <= 64 && g.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Invalid read-only source limits.");
}
