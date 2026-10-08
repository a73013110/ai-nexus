using AiNexus.Features.AccessControl;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Library;

/// <summary>Personal prompt templates. Each use case lives in its own file next to this module.</summary>
public sealed class LibraryModule : IFeatureModule
{
    public const int MaxTemplateCharacters = 12000;

    public static void AddServices(IHostApplicationBuilder builder) => builder.Services.AddScoped<SavePromptTemplate>();

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/prompt-templates").RequireAuthorization(BuiltInAccess.ChatPolicy).WithTags("PromptLibrary")
            .WithRequestBodyLimit(RequestBodyLimits.ForJsonCharacters(MaxTemplateCharacters));
        ListPromptTemplates.Map(routes);
        SavePromptTemplate.Map(routes);
        DeletePromptTemplate.Map(routes);
    }
}
