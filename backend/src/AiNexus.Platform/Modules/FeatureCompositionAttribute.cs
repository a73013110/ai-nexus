namespace AiNexus.Platform.Modules;

/// <summary>
/// 標記組件的組合點：<c>FeatureCompositionGenerator</c> 在編譯期為它實作下列 <c>private static partial</c> 方法，
/// 內容是組件內所有 <see cref="IFeatureModule"/>、端點切片與 <see cref="Validation.RequestValidator{T}"/>（ADR 0029），
/// 以及實作 <see cref="RegisterImplementationsAttribute"/> 介面、<c>IHostedService</c> 或標了 <see cref="ScopedAttribute"/>／<see cref="SingletonAttribute"/> 的類別（ADR 0033），依完整型別名稱排序。
/// 類別要是頂層的 <c>static partial</c>，並宣告：
/// <c>AddModules(IHostApplicationBuilder builder)</c>、<c>AddDeclaredServices(IServiceCollection services)</c>、<c>MapModules(RouteGroupBuilder api)</c>、<c>VerifyModules(IServiceProvider services)</c>、
/// <c>AddEndpointHandlers(IServiceCollection services)</c>、<c>AddRequestValidators(IServiceCollection services)</c>。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class FeatureCompositionAttribute : Attribute;
