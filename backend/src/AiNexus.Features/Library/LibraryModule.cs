using AiNexus.Platform.Modules;

namespace AiNexus.Features.Library;

public sealed class LibraryModule : IFeatureModule
{
    public const int MaxTemplateCharacters = 12000;

    public static void AddServices(IHostApplicationBuilder builder) => builder.Services.AddScoped<PromptLibraryService>();

    public static void MapEndpoints(RouteGroupBuilder api) => api.MapPromptLibrary();
}
