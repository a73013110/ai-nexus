using AiNexus.Generators.Common;

namespace AiNexus.Generators.Catalogs;

/// <summary>
/// 一個目錄的產出內容：方法與列入的欄位。只放符合元素型別的欄位，其他欄位的增刪不會改變它，輸出步驟沿用快取。
/// </summary>
internal sealed record Catalog
{
    private Catalog(CatalogTarget target, EquatableArray<string> references, EquatableArray<DiagnosticInfo> diagnostics)
    {
        Target = target;
        References = references;
        Diagnostics = diagnostics;
    }

    public CatalogTarget Target { get; }

    /// <summary>列入的欄位，產生的程式碼引用它們的寫法。</summary>
    public EquatableArray<string> References { get; }

    /// <summary>方法形狀不對（AINX2001／2003）或符合型別但不可見的欄位（AINX2002）。</summary>
    public EquatableArray<DiagnosticInfo> Diagnostics { get; }

    public static Catalog From(CatalogTarget target, EquatableArray<CatalogField> fields)
    {
        if (target.Problem is { } problem) return new(target, new([]), new([problem]));
        var matching = fields.Where(x => x.Type == target.ElementType).ToList();
        return new(target,
            new([.. matching.Where(x => x.Hidden is null).Select(x => x.Reference)]),
            new([.. matching.Where(x => x.Hidden is not null).Select(x => new DiagnosticInfo(CatalogDiagnostics.InvisibleField, x.Hidden, x.Display, target.Display))]));
    }
}
