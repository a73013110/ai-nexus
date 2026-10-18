using AiNexus.Data.App.Prompts;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Prompts;

/// <summary>使用者個人的提示詞範本。</summary>
public sealed class PromptsModule : IFeatureModule
{
    public const int MaxTemplateCharacters = PromptTemplate.ContentMaxLength;

    public static void AddServices(IHostApplicationBuilder builder) { }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/prompt-templates").RequireAuthorization(Policies.Chat).WithTags("PromptLibrary")
            .WithRequestBodyLimit(RequestBodyLimits.ForJsonCharacters(MaxTemplateCharacters));
        ListPromptTemplates.Map(routes);
        SavePromptTemplate.Map(routes);
        DeletePromptTemplate.Map(routes);
    }
}
