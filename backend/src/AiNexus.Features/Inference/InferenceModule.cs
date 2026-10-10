using AiNexus.Features.AccessControl;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

/// <summary>Model providers, catalog, policies and the generation queue. Other modules use <see cref="ModelTaskService"/>, <see cref="ModelCatalog"/> and <see cref="ModelPresentation"/>; each HTTP use case has its own file.</summary>
public sealed class InferenceModule : IFeatureModule
{
    /// <summary>Prompt-bearing requests scale with the configured input limit (never below the default JSON limit).</summary>
    public static long PromptBodyLimit(IServiceProvider services)
        => Math.Max(RequestBodyLimits.Default, RequestBodyLimits.ForJsonCharacters(services.GetRequiredService<IOptions<InferenceOptions>>().Value.MaxInputCharacters));

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<ModelPolicyService>();
        services.AddScoped<ModelTaskService>();
        services.AddSettings<InferenceOptions, InferenceOptionsValidator>(InferenceOptions.Section).Configure(x => x.RouteProviders());
        services.AddHttpClient("Ollama", (sp, client) =>
        {
            client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<InferenceOptions>>().Value.Providers.Ollama.Endpoint);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddHttpClient("GoogleAI", client => { client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"); client.Timeout = Timeout.InfiniteTimeSpan; });
        services.AddKeyedSingleton<IInferenceProvider>("google", (sp, _) => new GoogleAiProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleAI"), sp.GetRequiredService<IOptions<InferenceOptions>>()));
        services.AddKeyedSingleton<IInferenceProvider>("ollama", (sp, _) => new OllamaProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama")));
        services.AddSingleton<InferenceRouter>();
        services.AddSingleton<ModelCatalog>();
        services.AddSingleton<ModelPresentation>();
        services.AddSingleton<GenerationScheduler>();
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        ListModels.Map(api.MapGroup("").RequireAuthorization(Policies.Chat).WithTags("Inference"));
        GetStatus.Map(api);
    }
}
