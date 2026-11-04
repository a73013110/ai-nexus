using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Composition;

/// <summary>
/// 一個垂直切片（use case）：實作 <c>IEndpointSlice</c> 的具體類別。
/// 端點委派直接以切片類別為參數，所以切片本身要註冊為 scoped。
/// </summary>
internal sealed record SliceRegistration
{
    public const string Namespace = "AiNexus.Platform.Modules", Contract = "IEndpointSlice", Mounts = "EndpointSlices", Mount = "MapSlice";

    private SliceRegistration(string type, DiagnosticInfo? problem)
    {
        Type = type;
        Problem = problem;
    }

    public string Type { get; }

    public DiagnosticInfo? Problem { get; }

    public static SliceRegistration? From(INamedTypeSymbol type)
    {
        if (!IsSlice(type)) return null;
        var problem = Symbols.IsVisibleToAssembly(type) ? null
            : new DiagnosticInfo(CompositionDiagnostics.InvisibleType, LocationInfo.From(type), type.ToDisplayString(), "端點切片");
        return new(Symbols.FullName(type), problem);
    }

    /// <summary>
    /// 型別是不是切片。產生器據此註冊切片，<see cref="UnmappedSliceAnalyzer"/> 據此檢查每個切片都有掛載，兩者必須是同一條規則。
    /// </summary>
    public static bool IsSlice(INamedTypeSymbol type)
        => Symbols.IsConcreteClass(type) && Symbols.Interfaces(type, Namespace, Contract).Any();

    /// <summary>呼叫的是不是模組掛載切片的 <c>EndpointSlices.MapSlice&lt;T&gt;</c>。</summary>
    public static bool IsMount(IMethodSymbol method)
        => method is { Name: Mount, IsGenericMethod: true, TypeArguments.Length: 1 }
            && Symbols.Is(method.ContainingType, Namespace, Mounts);
}
