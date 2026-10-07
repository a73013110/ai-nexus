using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiNexus.BuildingBlocks.Diagnostics;

/// <summary>Render only approved scalar values. Never invoke the application's raw ILogger formatter.</summary>
public static partial class DiagnosticMessage
{
    public static string Render(DiagnosticEvent item, bool includeProperties = false)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (includeProperties)
        {
            try
            {
                using var document = JsonDocument.Parse(DiagnosticRedactor.RecoverProperties(item.PropertiesJson));
                foreach (var property in document.RootElement.EnumerateObject())
                    values[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(), JsonValueKind.True => true, JsonValueKind.False => false,
                        JsonValueKind.Number when property.Value.TryGetInt64(out var n) => n,
                        JsonValueKind.Number when property.Value.TryGetDouble(out var d) => d,
                        _ => null
                    };
            }
            catch (JsonException) { /* Historical malformed metadata must not break a query. */ }
        }
        // These are already part of the query permission's summary, excluding identity/client metadata.
        foreach (var (key, value) in new Dictionary<string, object?> {
            ["IssueCode"] = item.IssueCode, ["TraceId"] = item.TraceId, ["SpanId"] = item.SpanId,
            ["RequestId"] = item.RequestId, ["OperationId"] = item.OperationId, ["JobId"] = item.JobId,
            ["RunId"] = item.RunId, ["Attempt"] = item.Attempt, ["Method"] = item.Method, ["Route"] = item.Route,
            ["StatusCode"] = item.StatusCode, ["DurationMs"] = item.DurationMs, ["ExternalService"] = item.ExternalService,
            ["ErrorCode"] = item.ErrorCode, ["Code"] = item.ErrorCode, ["Provider"] = item.ExternalService,
            ["UntrustedClient"] = item.UntrustedClient
        }) values[key] = value;
        return Render(item.MessageTemplate, values);
    }
    public static string Render(string template, IReadOnlyDictionary<string, object?> values)
    {
        var safeTemplate = DiagnosticRedactor.Text(template, 2048);
        var message = Token().Replace(safeTemplate, match =>
        {
            if (match.Value == "{{") return "{";
            if (match.Value == "}}") return "}";
            if (!values.TryGetValue(match.Groups["name"].Value, out var value)) return "[omitted]";
            var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
            var text = value switch
            {
                null => "—", string s => DiagnosticRedactor.Text(s, 240), Guid id => id.ToString(),
                bool flag => flag ? "true" : "false",
                byte or short or int or long or uint or ulong or decimal or double or float => FormatNumber(value, format),
                _ => "[omitted]"
            };
            if (int.TryParse(match.Groups["alignment"].Value, out var alignment))
                text = alignment < 0 ? text.PadRight(Math.Min(-alignment, 80)) : text.PadLeft(Math.Min(alignment, 80));
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
