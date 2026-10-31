using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Common;

/// <summary>產生器共用的符號判斷。</summary>
internal static class Symbols
{
    /// <summary>產生的程式碼一律寫完整名稱（<c>global::</c> 開頭），不受使用端的 using 與同名型別影響。</summary>
    public static string FullName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>
    /// 是否為指定的型別（泛型比對定義，<paramref name="arity"/> 為型別參數數量）。
    /// 先比名稱再比 namespace，不必每個語法節點都向編譯查詢型別。
    /// </summary>
    public static bool Is(ITypeSymbol type, string ns, string name, int arity = 0)
        => type is INamedTypeSymbol named && named.Name == name && named.Arity == arity && named.ContainingNamespace.ToDisplayString() == ns;

    /// <summary>型別（含基底型別）實作的介面中，屬於指定介面的那些（泛型介面可能以不同型別引數實作多次）。</summary>
    public static IEnumerable<INamedTypeSymbol> Interfaces(INamedTypeSymbol type, string ns, string name, int arity = 0)
        => type.AllInterfaces.Where(x => Is(x, ns, name, arity));

    /// <summary>
    /// 同組件的產生程式碼能否引用此型別：型別、每一層外層型別與泛型引數都要是 public 或 internal。
    /// <c>file</c> 型別只在宣告檔可見，也不行。
    /// </summary>
    public static bool IsVisibleToAssembly(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named) return type.TypeKind is not TypeKind.Error;
        for (var current = named; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal) return false;
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)) return false;
        }
        return named.TypeArguments.All(IsVisibleToAssembly);
    }

    /// <summary>類別可以直接 new 出來（DI 建立）：不是 static、abstract，也不是開放泛型（本身或外層型別有型別參數）。</summary>
    public static bool IsConcreteClass(INamedTypeSymbol type)
    {
        if (type.TypeKind is not TypeKind.Class || type.IsStatic || type.IsAbstract) return false;
        for (var current = type; current is not null; current = current.ContainingType)
            if (current.TypeParameters.Length > 0) return false;
        return true;
    }
}
