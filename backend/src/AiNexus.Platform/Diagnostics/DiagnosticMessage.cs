using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 診斷訊息模板的安全渲染器。只渲染核准的純量數值與白名單中繼資料，不直接使用應用程式未受控的 ILogger 格式字串。
/// </summary>
public static partial class DiagnosticMessage
{
    /// <summary>
    /// 把日誌事件的模板，以安全參數渲染成文字。
    /// </summary>
    public static string Render(DiagnosticEvent item, bool includeProperties = false)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (includeProperties)
        {
            try
            {
                using var document = JsonDocument.Parse(DiagnosticRedactor.RecoverProperties(item.PropertiesJson));
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    values[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number when property.Value.TryGetInt64(out var n) => n,
                        JsonValueKind.Number when property.Value.TryGetDouble(out var d) => d,
                        _ => null
                    };
                }
            }
            catch (JsonException)
            {
                // 歷史遺留的格式錯誤中繼資料不可讓查詢中斷。
            }
        }

        // 以下欄位屬於查詢權限摘要的一部分，不含機敏的身分或客戶端 IP。
        var standardFields = new Dictionary<string, object?>
        {
            ["IssueCode"] = item.IssueCode,
            ["TraceId"] = item.TraceId,
            ["SpanId"] = item.SpanId,
            ["RequestId"] = item.RequestId,
            ["OperationId"] = item.OperationId,
            ["JobId"] = item.JobId,
            ["RunId"] = item.RunId,
            ["Attempt"] = item.Attempt,
            ["Method"] = item.Method,
            ["Route"] = item.Route,
            ["StatusCode"] = item.StatusCode,
            ["DurationMs"] = item.DurationMs,
            ["ExternalService"] = item.ExternalService,
            ["ErrorCode"] = item.ErrorCode,
            ["Code"] = item.ErrorCode,
            ["Provider"] = item.ExternalService,
            ["UntrustedClient"] = item.UntrustedClient
        };

        foreach (var (key, value) in standardFields)
        {
            values[key] = value;
        }

        return Render(item.MessageTemplate, values);
    }

    /// <summary>
    /// 依安全字典替換模板中的具名欄位；找不到的欄位以 [omitted] 遮蔽。
    /// </summary>
    public static string Render(string template, IReadOnlyDictionary<string, object?> values)
    {
        var safeTemplate = DiagnosticRedactor.Text(template, 2048);
        var message = Token().Replace(safeTemplate, match =>
        {
            if (match.Value == "{{")
            {
                return "{";
            }
            if (match.Value == "}}")
            {
                return "}";
            }

            if (!values.TryGetValue(match.Groups["name"].Value, out var value))
            {
                return "[omitted]";
            }

            var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
            var text = value switch
            {
                null => "—",
                string s => DiagnosticRedactor.Text(s, 240),
                Guid id => id.ToString(),
                bool flag => flag ? "true" : "false",
                byte or short or int or long or uint or ulong or decimal or double or float => FormatNumber(value, format),
                _ => "[omitted]"
            };

            if (int.TryParse(match.Groups["alignment"].Value, out var alignment))
            {
                text = alignment < 0
                    ? text.PadRight(Math.Min(-alignment, 80))
                    : text.PadLeft(Math.Min(alignment, 80));
            }

            return text;
        });

        return DiagnosticRedactor.Text(message, 2048);
    }
    private static string FormatNumber(object value, string? format)
    {
        if (format is not null && !NumberFormat().IsMatch(format)) return "[invalid format]";
        try { return ((IFormattable)value).ToString(format ?? (value is double or float ? "0.###" : null), CultureInfo.InvariantCulture); }
        catch (FormatException) { return "[invalid format]"; }
    }
    [GeneratedRegex(@"^(?:[NnFfGgEePpDdXx](?:\d|[12]\d)?|[0#.,% ]{1,32})$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberFormat();
    [GeneratedRegex(@"\{\{|\}\}|\{(?<name>[A-Za-z][A-Za-z0-9_.]*)(?:,(?<alignment>-?\d{1,2}))?(?::(?<format>[^{}]{1,32}))?\}", RegexOptions.CultureInvariant)]
    private static partial Regex Token();
}
