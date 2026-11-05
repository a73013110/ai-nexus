using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.Assistant;

/// <summary>
/// 全站助理：一個提問端點（<see cref="AskAssistant"/>），可以問什麼由各模組登記的 <see cref="IAssistantTool"/> 決定。
/// 授權跟著工具：端點只要登入，交給模型的只有提問者持有功能的工具，工具以提問者的資料範圍執行並各自稽核。
/// </summary>
public sealed class AssistantModule : IFeatureModule
{
    /// <summary>提問的限流：每次提問都是一到數次模型呼叫，連點或腳本不能放大成本。</summary>
    public const string RateLimit = "assistant";

    /// <summary>提問的 body 上限：提問加上最多的先前輪次與畫面條件（<see cref="AssistantQuestionValidator"/>）。</summary>
    public static readonly long BodyLimit = RequestBodyLimits.ForJsonCharacters(AssistantQuestionValidator.MaxCharacters);

    public static void AddServices(IHostApplicationBuilder builder)
        => builder.Services.AddRateLimiter(options => options.AddPerUserLimit(RateLimit, 12));

    public static void MapEndpoints(RouteGroupBuilder api)
        => api.MapGroup("/assistant").MapSlice<AskAssistant>();
}
