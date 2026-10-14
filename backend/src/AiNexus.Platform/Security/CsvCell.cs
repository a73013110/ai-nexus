namespace AiNexus.Platform.Security;

/// <summary>CSV exports opened in a spreadsheet: every cell is quoted, and text that would start a formula is prefixed with <c>'</c>.</summary>
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
