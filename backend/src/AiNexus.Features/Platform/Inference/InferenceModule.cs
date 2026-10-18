using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Inference.ModelPolicies;
using AiNexus.Features.Platform.Inference.ModelTasks;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.Inference.Providers;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Inference;

/// <summary>
/// 推論子系統模組。
/// 封裝推論供應商接線、模型目錄、配額與安全性原則及非同步生成排程佇列。
/// 其他業務模組主要透過 <see cref="ModelTaskService"/>、<see cref="ModelCatalog"/> 與 <see cref="ModelPresentation"/> 進行互動。
/// </summary>
public sealed class InferenceModule : IFeatureModule
{
    /// <summary>
    /// 包含提問提示詞之請求主體大小上限，依組態配置之最大輸入字元數彈性換算（絕不低於預設 JSON 配額）。
    /// </summary>
    public static long PromptBodyLimit(IServiceProvider services)
        => Math.Max(RequestBodyLimits.Default, RequestBodyLimits.ForJsonCharacters(services.GetRequiredService<IOptions<InferenceOptions>>().Value.MaxInputCharacters));

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<ModelPolicyService>();
        services.AddScoped<ModelTaskService>();
        services.AddSettings<InferenceOptions, InferenceOptionsValidator>(InferenceOptions.Section)
            .Configure(x => x.RouteProviders());

        services.AddHttpClient("Ollama", (sp, client) =>
        {
            client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<InferenceOptions>>().Value.Providers.Ollama.Endpoint);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddHttpClient("GoogleAI", client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddKeyedSingleton<IInferenceProvider>("google", (sp, _) =>
            new GoogleAiProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleAI"), sp.GetRequiredService<IOptions<InferenceOptions>>()));

        services.AddKeyedSingleton<IInferenceProvider>("ollama", (sp, _) =>
            new OllamaProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama")));

        services.AddSingleton<InferenceRouter>();
        services.AddSingleton<ModelCatalog>();
        services.AddSingleton<ModelPresentation>();
        services.AddSingleton<GenerationScheduler>();
        services.AddMonitoringSource<GenerationQueueMonitoring>();
        services.AddMonitoringSource<ModelProvidersMonitoring>();
    }

    /// <summary>
    /// 註冊推論模組之 API 端點（順序即為 OpenAPI 發布順序）。
    /// </summary>
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        ListModels.Map(api.MapGroup("").RequireAuthorization(Policies.Chat).WithTags("Inference"));
        GetStatus.Map(api);
        GetEffectiveModelPolicy.Map(api);
    }

    /// <summary>管理端的使用者模型策略。由 <c>FeatureModules</c> 與其他管理端點一起掛載，理由見該處註解。</summary>
    public static void MapAdministration(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization(Policies.Admin).WithTags("Administration");
        GetUserModelPolicy.Map(admin);
        SetUserModelPolicy.Map(admin);
    }
}
