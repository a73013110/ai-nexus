using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Modules;

/// <summary>
/// 自動註冊垂直切片（Vertical Slice）的處理常式。
/// 每個使用案例都是獨立的 <c>internal sealed class</c>：靜態 <c>Map</c> 方法宣告路由，執行個體的 <c>HandleAsync</c> 執行邏輯，依賴由建構子注入。
/// 端點委派會直接以切片類別為參數，這裡依方法簽章掃描並註冊為 Scoped，因此新增切片不需要手動維護清單。
/// </summary>
public static class EndpointHandlers
{
    public static IServiceCollection AddEndpointHandlers(this IServiceCollection services, Assembly assembly)
    {
        foreach (var slice in Slices(assembly).Where(type => !type.IsAbstract)) services.TryAddScoped(slice);
        return services;
    }

    /// <summary>列出組件中所有含 Map 方法（把端點掛到模組路由群組）的垂直切片型別。</summary>
    public static IEnumerable<Type> Slices(Assembly assembly) => assembly.GetTypes().Where(type => MapMethods(type).Any());

    public static IEnumerable<MethodInfo> MapMethods(Type type) => type
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(method => method.Name.StartsWith("Map", StringComparison.Ordinal) && !method.IsGenericMethodDefinition
            && method.GetParameters() is [{ ParameterType: var routes }, ..] && routes == typeof(RouteGroupBuilder))
        .Where(_ => !typeof(IFeatureModule).IsAssignableFrom(type));
}
