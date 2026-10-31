using System.Reflection;
using AiNexus.Platform.Modules;
using Microsoft.AspNetCore.Routing;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 以反射重述「切片」的定義：有 static <c>Map*</c> 方法、第一個參數是 <see cref="RouteGroupBuilder"/> 的類別；模組與組合點（<see cref="FeatureCompositionAttribute"/>）形狀相同但不是切片。
/// 組合產生器用同一條規則辨識切片，<see cref="CompositionTests"/> 拿兩邊的結果互相對照。
/// </summary>
internal static class Slices
{
    public static IEnumerable<Type> In(Assembly assembly) => assembly.GetTypes().Where(type => MapMethods(type).Any());

    private static IEnumerable<MethodInfo> MapMethods(Type type) => type
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(method => method.Name.StartsWith("Map", StringComparison.Ordinal) && !method.IsGenericMethodDefinition
            && method.GetParameters() is [{ ParameterType: var routes }, ..] && routes == typeof(RouteGroupBuilder))
        .Where(_ => !typeof(IFeatureModule).IsAssignableFrom(type) && !type.IsDefined(typeof(FeatureCompositionAttribute), inherit: false));
}
