using Microsoft.CodeAnalysis;

namespace AiNexus.Generators.Catalogs;

/// <summary>欄位目錄產生器的診斷，代碼 AINX2xxx（各產生器的代碼區段見 docs/architecture/source-generators.md）。</summary>
internal static class CatalogDiagnostics
{
    private const string Category = "AiNexus.Catalogs";

    public static readonly DiagnosticDescriptor InvalidTarget = new(
        "AINX2001", "欄位目錄方法的形狀不對",
        "'{0}' 標了 [FieldCatalog]，但不是宣告在頂層非泛型 partial 類別、沒有參數、回傳 T[] 或 IReadOnlyList<T> 的 static partial 方法", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvisibleField = new(
        "AINX2002", "列入目錄的欄位必須在組件內可見",
        "'{0}' 的型別符合目錄 '{1}'，但它的類別或外層類別不是 public／internal，產生的程式碼無法引用", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);
}
