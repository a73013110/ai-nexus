using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNexus.Generators.Composition;

/// <summary>標了 <c>[FeatureComposition]</c> 的類別：產生的 partial 方法實作寫進這裡。</summary>
internal sealed record CompositionTarget
{
    private CompositionTarget(string? ns, string name, string display, LocationInfo? location, bool valid)
    {
        Namespace = ns;
        Name = name;
        Display = display;
        Location = location;
        Valid = valid;
    }

    /// <summary>所在 namespace；全域 namespace 為 null。</summary>
    public string? Namespace { get; }

    public string Name { get; }

    /// <summary>診斷訊息用的完整名稱。</summary>
    public string Display { get; }

    public LocationInfo? Location { get; }

    /// <summary>是頂層的 static partial 類別；不是時只回報診斷、不產生程式碼。</summary>
    public bool Valid { get; }

    public static CompositionTarget From(GeneratorAttributeSyntaxContext context)
    {
        var type = (INamedTypeSymbol)context.TargetSymbol;
        var partial = ((ClassDeclarationSyntax)context.TargetNode).Modifiers.Any(SyntaxKind.PartialKeyword);
        return new(type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(), type.Name, type.ToDisplayString(),
            LocationInfo.From(type), partial && type.IsStatic && type.ContainingType is null);
    }
}
