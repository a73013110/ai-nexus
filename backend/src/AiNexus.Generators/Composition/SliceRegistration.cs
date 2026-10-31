using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Composition;

/// <summary>
/// 一個垂直切片（use case）：類別有 static <c>Map*</c> 方法、第一個參數是 <c>RouteGroupBuilder</c>，
/// 端點委派直接以切片類別為參數，所以切片本身要註冊為 scoped。
/// </summary>
internal sealed record SliceRegistration
{
    private SliceRegistration(string type, DiagnosticInfo? problem)
    {
        Type = type;
        Problem = problem;
    }

    public string Type { get; }

    public DiagnosticInfo? Problem { get; }

    public static SliceRegistration? From(IMethodSymbol method)
    {
        if (!IsMapMethod(method)) return null;
        var type = method.ContainingType;
        var problem = Symbols.IsVisibleToAssembly(type) ? null
            : new DiagnosticInfo(CompositionDiagnostics.InvisibleType, LocationInfo.From(type), type.ToDisplayString(), "端點切片");
        return new(Symbols.FullName(type), problem);
    }

    /// <summary>
    /// 方法是不是切片的掛載方法。產生器據此註冊切片，<see cref="UnmappedSliceAnalyzer"/> 據此檢查每個掛載方法都有人呼叫，兩者必須是同一條規則。
    /// </summary>
    public static bool IsMapMethod(IMethodSymbol method)
        => method is { MethodKind: MethodKind.Ordinary, IsStatic: true, IsGenericMethod: false, Parameters.Length: > 0 }
            && method.Name.StartsWith("Map", StringComparison.Ordinal)
            && Symbols.Is(method.Parameters[0].Type, "Microsoft.AspNetCore.Routing", "RouteGroupBuilder")
            && Symbols.IsConcreteClass(method.ContainingType)
            // 模組的 MapEndpoints 形狀相同，但模組是組合點、不是切片。
            && !Symbols.Interfaces(method.ContainingType, ModuleRegistration.Namespace, ModuleRegistration.Contract).Any();
}
