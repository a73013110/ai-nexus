namespace AiNexus.Platform.Security;

/// <summary>供試算表軟體開啟的 CSV 輸出防護：每個儲存格皆以雙引號包裹；若開頭字元可能觸發公式計算（=, +, -, @），則前綴單引號以防止公式注入（CSV Formula Injection）。</summary>
public static class CsvCell
{
    private static readonly char[] FormulaStarts = ['=', '+', '-', '@'];

    public static string Quote(string? text)
    {
        var value = text ?? "";
        var trimmed = value.TrimStart();
        return "\"" + (trimmed.Length > 0 && FormulaStarts.Contains(trimmed[0]) ? "'" : "") + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
