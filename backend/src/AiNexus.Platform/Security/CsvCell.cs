namespace AiNexus.Platform.Security;

/// <summary>
/// 匯出 CSV 時的儲存格防護。每格都以雙引號包住；開頭字元若可能被試算表當成公式（=、+、-、@），就前綴單引號，防止 CSV 公式注入。
/// </summary>
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
