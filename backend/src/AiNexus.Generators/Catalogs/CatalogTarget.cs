using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNexus.Generators.Catalogs;

/// <summary>標了 <c>[FieldCatalog]</c> 的方法：產生的實作回傳型別為 <see cref="ElementType"/> 的欄位。</summary>
internal sealed record CatalogTarget
{
    private CatalogTarget(string? ns, string declaration, string signature, string elementType, string display, DiagnosticInfo? problem)
    {
        Namespace = ns;
        Declaration = declaration;
        Signature = signature;
        ElementType = elementType;
        Display = display;
        Problem = problem;
    }

    /// <summary>所在 namespace；全域 namespace 為 null。</summary>
    public string? Namespace { get; }

    /// <summary>外層類別的 partial 宣告，例如 <c>static partial class AuditActions</c>。</summary>
    public string Declaration { get; }

    /// <summary>方法的實作簽章（存取修飾、回傳型別與名稱），與使用端的宣告一致。</summary>
    public string Signature { get; }

    /// <summary>元素型別的完整名稱；欄位型別與它相同才列入。</summary>
    public string ElementType { get; }

    /// <summary>診斷訊息與產出檔名用的名稱（類別.方法）。</summary>
    public string Display { get; }

    /// <summary>形狀不對（AINX2001）或元素型別太通用（AINX2003）時的診斷；有診斷時不產生程式碼。</summary>
    public DiagnosticInfo? Problem { get; }

    public static CatalogTarget From(GeneratorAttributeSyntaxContext context)
    {
        var method = (IMethodSymbol)context.TargetSymbol;
        var syntax = (MethodDeclarationSyntax)context.TargetNode;
        var type = method.ContainingType;
        var element = ElementOf(method.ReturnType);
        var valid = element is not null && method is { IsStatic: true, IsGenericMethod: false, Parameters.Length: 0 }
            && syntax.Modifiers.Any(SyntaxKind.PartialKeyword) && syntax is { Body: null, ExpressionBody: null }
            && type is { TypeKind: TypeKind.Class, ContainingType: null, IsGenericType: false }
            && type.DeclaringSyntaxReferences.All(x => x.GetSyntax() is ClassDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
        var display = $"{type.ToDisplayString()}.{method.Name}";
        var problem = !valid ? new DiagnosticInfo(CatalogDiagnostics.InvalidTarget, LocationInfo.From(method), display)
            : IsGeneral(element!) ? new DiagnosticInfo(CatalogDiagnostics.GeneralElement, LocationInfo.From(method), display, element!.ToDisplayString())
            : null;
        return new(type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            $"{(type.IsStatic ? "static " : "")}partial class {type.Name}",
            $"{SyntaxFacts.GetText(method.DeclaredAccessibility)} static partial {Symbols.FullName(method.ReturnType)} {method.Name}()",
            element is null ? "" : Symbols.FullName(element), display, problem);
    }

    // 規則只看欄位型別：string、int、object 這類型別在別處也會宣告成 public static 欄位，會被誤列入目錄。
    private static bool IsGeneral(ITypeSymbol element) => element.SpecialType != SpecialType.None || element.TypeKind is TypeKind.Interface or TypeKind.TypeParameter;

    private static ITypeSymbol? ElementOf(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol { Rank: 1 } array => array.ElementType,
        INamedTypeSymbol named when Symbols.Is(named, "System.Collections.Generic", "IReadOnlyList", arity: 1) => named.TypeArguments[0],
        _ => null,
    };
}
