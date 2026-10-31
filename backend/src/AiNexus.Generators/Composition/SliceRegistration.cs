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
        if (!method.IsStatic || method.IsGenericMethod || method.Parameters.Length == 0
            || !Symbols.Is(method.Parameters[0].Type, "Microsoft.AspNetCore.Routing", "RouteGroupBuilder")) return null;
        var type = method.ContainingType;
        // 模組的 MapEndpoints 形狀相同，但模組是組合點、不是切片。
        if (!Symbols.IsConcreteClass(type) || Symbols.Interfaces(type, ModuleRegistration.Namespace, ModuleRegistration.Contract).Any()) return null;
        var problem = Symbols.IsVisibleToAssembly(type) ? null
            : new DiagnosticInfo(CompositionDiagnostics.InvisibleType, LocationInfo.From(type), type.ToDisplayString(), "端點切片");
        return new(Symbols.FullName(type), problem);
    }
}
