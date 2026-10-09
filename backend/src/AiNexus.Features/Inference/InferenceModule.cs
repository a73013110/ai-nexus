using AiNexus.Features.AccessControl;
using AiNexus.Features.Configuration;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

/// <summary>Models, context previews and chat runs. Other modules use <see cref="ModelTaskService"/>, <see cref="ModelCatalog"/> and <see cref="ModelPresentation"/>; each HTTP use case has its own file.</summary>
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
        services.AddScoped<RunService>();
        services.AddScoped<PreviewContext>();
        services.AddScoped<CreateRun>();
        services.AddScoped<CancelRun>();
        services.AddScoped<RunLeaseRecovery>();
        services.AddScoped<ContextBuilder>();
        services.AddOptions<InferenceOptions>().Configure<IConfiguration>((o, c) => NexusSettings.Inference(c, o)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<InferenceOptions>, InferenceOptionsValidator>();
        services.AddHttpClient("Ollama", (sp, client) =>
        {
            client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<InferenceOptions>>().Value.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddHttpClient("GoogleAI", client => { client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"); client.Timeout = Timeout.InfiniteTimeSpan; });
        services.AddKeyedSingleton<IInferenceProvider>("google", (sp, _) => new GoogleAiProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleAI"), sp.GetRequiredService<IOptions<InferenceOptions>>()));
        services.AddKeyedSingleton<IInferenceProvider>("ollama", (sp, _) => new OllamaProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama")));
        services.AddSingleton<InferenceRouter>();
        services.AddSingleton<ModelCatalog>();
        services.AddSingleton<ModelPresentation>();
        services.AddSingleton<GenerationScheduler>();
        services.AddSingleton<SubscriptionLimits>();
        services.AddSingleton<RunSignals>();
        services.AddHostedService<GenerationWorker>();
        services.AddHostedService<RunRecoveryWorker>();
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("").RequireAuthorization(Policies.Chat).WithTags("Inference");
        ListModels.Map(routes);
        PreviewContext.Map(routes);
        CreateRun.Map(routes);
        GetRun.Map(routes);
        CancelRun.Map(routes);
        RunEventsEndpoint.Map(routes);
    }
}

internal sealed class InferenceOptionsValidator : IValidateOptions<InferenceOptions>
{
    public ValidateOptionsResult Validate(string? name, InferenceOptions x)
    {
        List<string> failures = [];
        if (!(x.ProviderConcurrency.Count > 0 && x.ProviderConcurrency.All(p => p.Key is "google" or "ollama" && p.Value is >= 1 and <= 8)))
            failures.Add("Invalid enabled inference providers or concurrency.");
        if (!((x.DefaultModelId is null || x.Models.Any(m => m.Id == x.DefaultModelId)) && x.Models.All(m => x.ProviderConcurrency.ContainsKey(m.Provider) && m.ValidReasoning(m.Provider))))
            failures.Add("Invalid default model or reasoning capabilities.");
        if (!(Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var url) && (url.Scheme is "http" or "https") && string.IsNullOrEmpty(url.UserInfo)))
            failures.Add("Inference BaseUrl must be a server-controlled HTTP endpoint.");
        if (!(x.QueueCapacity is >= 1 and <= 64 && x.TimeoutSeconds is >= 5 and <= 600 && x.MaxInputCharacters is >= 100 and <= 32000 && x.MaxOutputCharacters is >= 4096 and <= 262144))
            failures.Add("Invalid inference capacity or limits.");
        if (!(x.Models.Count > 0 && x.Models.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() == x.Models.Count
              && x.Models.All(m => !string.IsNullOrWhiteSpace(m.Id) && m.Id.Length <= 160 && m.NativeId.Length is > 0 and <= 150
                  && m.NativeId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.' or '/') && m.ContextTokens is >= 1024 and <= 32768
                  && m.MaxOutputTokens >= 128 && m.MaxOutputTokens < m.ContextTokens && m.SupportsStreaming)))
            failures.Add("Invalid model profiles.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
