using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.Assistant;

/// <summary>
/// 頁內助理：頁面把自己算好的事實交給 <see cref="AssistantResponder"/>，由模型依事實回答使用者的提問。
/// 本模組沒有端點；每個頁面在自己的模組掛一個提問端點（授權、資料範圍與稽核跟著那一頁），body 是 <see cref="AssistantQuestion"/>。
/// </summary>
public sealed class AssistantModule : IFeatureModule
{
    /// <summary>提問端點共用的限流：每次提問都是一次模型呼叫，連點或腳本不能放大成本。</summary>
    public const string RateLimit = "assistant";

    /// <summary>提問端點的 body 上限：提問加上最多的先前輪次（<see cref="AssistantQuestionValidator"/>）。</summary>
    public static readonly long BodyLimit = RequestBodyLimits.ForJsonCharacters(AssistantQuestionValidator.MaxCharacters);

    public static void AddServices(IHostApplicationBuilder builder)
        => builder.Services.AddRateLimiter(options => options.AddPerUserLimit(RateLimit, 12));
}
