using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Modules;

/// <summary>
/// 使用案例（Use Case）垂直切片（Vertical Slice）處理常式自動註冊器。
/// 各使用案例皆為獨立的 <c>internal sealed class</c>：以靜態 <c>Map</c> 方法宣告路由端點，並由執行個體的 <c>HandleAsync</c> 執行邏輯（依賴項由建構子注入）。
/// 端點委派直接將切片類別作為參數接收，此處依據方法特徵自動掃描並註冊為 Scoped，新增切片時完全無須手動維護註冊清單。
/// </summary>
public static class EndpointHandlers
{
    public static IServiceCollection AddEndpointHandlers(this IServiceCollection services, Assembly assembly)
    {
        foreach (var slice in Slices(assembly).Where(type => !type.IsAbstract)) services.TryAddScoped(slice);
        return services;
    }

    /// <summary>取得組件中所有將端點掛載至模組路由群組的垂直切片型別清單。</summary>
    public static IEnumerable<Type> Slices(Assembly assembly) => assembly.GetTypes().Where(type => MapMethods(type).Any());

    public static IEnumerable<MethodInfo> MapMethods(Type type) => type
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(method => method.Name.StartsWith("Map", StringComparison.Ordinal) && !method.IsGenericMethodDefinition
            && method.GetParameters() is [{ ParameterType: var routes }, ..] && routes == typeof(RouteGroupBuilder))
        .Where(_ => !typeof(IFeatureModule).IsAssignableFrom(type));
}
