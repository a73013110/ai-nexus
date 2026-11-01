using System.Collections.Immutable;
using AiNexus.Generators.Common;
using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Catalogs;

/// <summary>static 類別裡的一個 public static 欄位；型別符合某個目錄的元素型別時列入該目錄。</summary>
internal sealed record CatalogField
{
    private CatalogField(string order, int index, string reference, string type, string display, LocationInfo? hidden)
    {
        Order = order;
        Index = index;
        Reference = reference;
        Type = type;
        Display = display;
        Hidden = hidden;
    }

    /// <summary>類別的中繼資料完整名稱（巢狀型別以 <c>+</c> 連接），與 <c>Type.FullName</c> 的排序相同。</summary>
    public string Order { get; }

    /// <summary>欄位在類別裡的宣告順序。用序號而不是原始碼位置，方法內容的編輯才不會讓轉換結果改變。</summary>
    public int Index { get; }

    /// <summary>產生的程式碼引用它的寫法（<c>global::</c> 開頭）。</summary>
    public string Reference { get; }

    /// <summary>欄位型別的完整名稱。</summary>
    public string Type { get; }

    public string Display { get; }

    /// <summary>類別或外層類別不可見時的位置；可見時為 null。列入目錄時才回報 AINX2002。</summary>
    public LocationInfo? Hidden { get; }

    public static ImmutableArray<CatalogField> From(IEnumerable<IFieldSymbol> fields)
    {
        var builder = ImmutableArray.CreateBuilder<CatalogField>();
        foreach (var field in fields)
        {
            var type = field.ContainingType;
            if (field is not { IsStatic: true, DeclaredAccessibility: Accessibility.Public } || !type.IsStatic || IsGeneric(type)) continue;
            var index = type.GetMembers().OfType<IFieldSymbol>().TakeWhile(x => !SymbolEqualityComparer.Default.Equals(x, field)).Count();
            builder.Add(new(MetadataName(type), index, $"{Symbols.FullName(type)}.{field.Name}", Symbols.FullName(field.Type),
                $"{type.ToDisplayString()}.{field.Name}", Symbols.IsVisibleToAssembly(type) ? null : LocationInfo.From(field)));
        }
        return builder.ToImmutable();
    }

    private static bool IsGeneric(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
            if (current.TypeParameters.Length > 0) return true;
        return false;
    }

    private static string MetadataName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType) name = outer.MetadataName + "+" + name;
        return type.ContainingNamespace.IsGlobalNamespace ? name : type.ContainingNamespace.ToDisplayString() + "." + name;
    }
}
