using System.Reflection;
using AiNexus.Platform.Modules;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 以反射重述「切片」的定義：實作 <see cref="IEndpointSlice"/> 的類別。
/// 組合產生器用同一條規則辨識切片，<see cref="CompositionTests"/> 拿兩邊的結果互相對照。
/// </summary>
internal static class Slices
{
    public static IEnumerable<Type> In(Assembly assembly) => assembly.GetTypes().Where(type => type.IsClass && typeof(IEndpointSlice).IsAssignableFrom(type));
}
