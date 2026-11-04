using AiNexus.Data.App.Prompts;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Prompts;

/// <summary>使用者個人的提示詞範本。</summary>
public sealed class PromptsModule : IFeatureModule
{
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/prompt-templates").RequireFeature(FeatureCatalog.Chat)
            .WithRequestBodyLimit(RequestBodyLimits.ForJsonCharacters(PromptTemplate.ContentMaxLength))
            .MapSlice<ListPromptTemplates>()
            .MapSlice<SavePromptTemplate>()
            .MapSlice<DeletePromptTemplate>();
    }
}
