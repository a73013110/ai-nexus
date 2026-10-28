using System.Globalization;
using System.Text;

namespace AiNexus.Platform.Export;

/// <summary>
/// 把表格寫成 CSV（UTF-8 含 BOM、CRLF），供各列表的「匯出 CSV」共用；有 BOM，Excel 直接開啟中文才不會亂碼。
/// </summary>
/// <remarks>
/// 每格都以雙引號包住。文字開頭若可能被試算表當成公式（=、+、-、@），前綴單引號防止 CSV 公式注入；
/// 數字、日期等非字串的值以不變文化寫出、不加前綴，負數才不會被改成文字。
/// </remarks>
public static class Csv
{
    public const string ContentType = "text/csv; charset=utf-8";

    private static readonly char[] FormulaStarts = ['=', '+', '-', '@'];

    public static byte[] Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var csv = new StringBuilder("﻿");
        Line(csv, header);
        foreach (var row in rows) Line(csv, row);
        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private static void Line<T>(StringBuilder csv, IReadOnlyList<T> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0) csv.Append(',');
            csv.Append(Cell(cells[i]));
        }
        csv.Append("\r\n");
    }

    private static string Cell(object? value)
    {
        var (text, guard) = value switch
        {
            null => ("", false),
            string s => (s, true),
            IFormattable formattable => (formattable.ToString(null, CultureInfo.InvariantCulture), false),
            _ => (value.ToString() ?? "", true),
        };
        var trimmed = text.TrimStart();
        var prefix = guard && trimmed.Length > 0 && FormulaStarts.Contains(trimmed[0]) ? "'" : "";
        return "\"" + prefix + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
