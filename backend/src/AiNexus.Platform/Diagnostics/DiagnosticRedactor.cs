using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using AiNexus.Platform.Errors;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 診斷資料的脫敏過濾器。只允許明確核准的純量中繼資料離開日誌邊界，並遮蔽金鑰、權杖、密碼、URL 與檔案路徑。
/// </summary>
public static partial class DiagnosticRedactor
{
    private static readonly HashSet<string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        "IssueCode", "TraceId", "SpanId", "RequestId", "OperationId", "JobId", "RunId", "UserId", "Attempt", "Method", "Route", "StatusCode", "DurationMs",
        "ExternalService", "ErrorCode", "ErrorType", "Code", "SqlError", "SqlNumber", "RequestedMode", "ActualMode", "Reason", "Stage", "Kind", "Count", "RetryCount", "ElapsedMs", "Provider", "ProfileId", "Dimensions", "UntrustedClient", "ClientKind", "ClientFingerprint", "AttachmentId", "DocumentId", "CollectionId", "ResourceId", "ResourceType",
        "ClientAddress", "UserAgent", "RequestProtocol", "RequestScheme", "RequestOutcome", "RequestAborted", "ResponseStarted"
    };

    /// <summary>
    /// 文字脫敏：移除機密、URL、實體路徑與電子郵件，把控制字元換成空白，並截斷到上限長度。
    /// </summary>
    public static string Text(string? value, int limit = 512)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        // 先限制正規表示式的輸入大小，再把 CR／LF 與控制字元換成空白，避免終端機跳脫序列注入。
        value = value[..Math.Min(value.Length, 16000)];
        value = Secret().Replace(value, "$1=[REDACTED]");
        value = Bearer().Replace(value, "[REDACTED]");
        value = Url().Replace(value, "[URL REDACTED]");
        value = PathPattern().Replace(value, "[PATH REDACTED]");
        value = Email().Replace(value, "[IDENTITY REDACTED]");
        value = new string(value.Select(c => char.IsControl(c) || c is '\u2028' or '\u2029' ? ' ' : c).ToArray());
        return value[..Math.Min(value.Length, limit)];
    }

    /// <summary>
    /// 屬性字典脫敏：丟棄未核准的欄位名稱，只保留白名單內的純量欄位，最多 32 個。
    /// </summary>
    public static Dictionary<string, object?> Properties(IEnumerable<KeyValuePair<string, object?>> values)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values.Take(128))
        {
            if (!Fields.Contains(key) || result.Count >= 32)
            {
                continue;
            }

            result[key] = value switch
            {
                null => null,
                Guid id => id.ToString(),
                bool b => b,
                byte or short or int or long or uint or ulong or decimal => value,
                double d when double.IsFinite(d) => d,
                float f when float.IsFinite(f) => f,
                string s => Text(s, 240),
                Enum e => Text(e.ToString(), 80),
                _ => "[OBJECT OMITTED]"
            };
        }
        return result;
    }

    /// <summary>
    /// 例外脫敏：不保留錯誤訊息的自由文字（可能含 SQL 或提示詞內容），只保留型別、錯誤代碼與前 30 層堆疊框架。
    /// </summary>
    public static string Exception(Exception? exception)
    {
        if (exception is null)
        {
            return "";
        }

        // 例外訊息可能含有任意提示詞、SQL 值或第三方服務回應，因此不持久化。
        // 強型別代碼與堆疊框架已足夠用來診斷，不必冒險信任自由文字。
        var parts = new List<string>();
        for (var e = exception; e is not null && parts.Count < 6; e = e.InnerException)
        {
            var code = e is SqlException sql
                ? $" SQL number={sql.Number}, state={sql.State}, class={sql.Class}"
                : e is ExternalServiceException external
                    ? " code=" + Text(external.Error.Code, 80)
                    : "";

            var frames = new StackTrace(e, false).GetFrames()?
                .Take(30)
                .Select(f =>
                {
                    var method = f.GetMethod();
                    return Text(method?.DeclaringType?.FullName + "." + method?.Name, 240);
                }) ?? [];

            parts.Add(Text(e.GetType().FullName, 180) + code + " [message omitted]\n" + string.Join("\n", frames));
        }

        var joined = string.Join("\nCaused by: ", parts);
        return joined[..Math.Min(joined.Length, 12000)];
    }

    /// <summary>
    /// 把中繼資料序列化為 JSON，並確保不超過 8 KB；超過時從尾端逐一移除欄位。
    /// </summary>
    public static string Json(Dictionary<string, object?> properties)
    {
        while (JsonSerializer.Serialize(properties).Length > 8192 && properties.Count > 0)
        {
            properties.Remove(properties.Keys.Last());
        }
        return JsonSerializer.Serialize(properties);
    }

    /// <summary>
    /// 從 JSON 還原屬性字典，並重新套用白名單與型別檢查。
    /// </summary>
    public static string RecoverProperties(string? json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > 8192)
        {
            return "{}";
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return "{}";
        }

        return Json(Properties(document.RootElement.EnumerateObject().Take(32).Select(p => new KeyValuePair<string, object?>(p.Name, p.Value.ValueKind switch
        {
            JsonValueKind.String => p.Value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when p.Value.TryGetInt64(out var n) => n,
            JsonValueKind.Number when p.Value.TryGetDouble(out var d) && double.IsFinite(d) => d,
            _ => null
        }))));
    }

    /// <summary>
    /// 在持久化與重放的邊界再套用一次：收斂長度、正規化欄位，避免惡意內容污染 SQL 中的紀錄。
    /// </summary>
    public static void Normalize(DiagnosticEvent item)
    {
        item.At = item.At.ToUniversalTime();
        item.Category = Text(item.Category, 180);
        item.EventName = Text(item.EventName, 100);
        item.MessageTemplate = Text(item.MessageTemplate, 2048);
        item.Service = Text(item.Service, 80);
        item.Environment = Text(item.Environment, 32);
        item.Version = Text(item.Version, 80);
        item.Instance = Text(item.Instance, 100);
        item.PropertiesJson = RecoverProperties(item.PropertiesJson);
        item.IssueCode = Issues.ValidCode(item.IssueCode) ? item.IssueCode : null;
        item.TraceId = Hex(item.TraceId, 32);
        item.SpanId = Hex(item.SpanId, 16);
        item.RequestId = item.RequestId is null ? null : Text(item.RequestId, 40);
        item.Method = item.Method is null ? null : Text(item.Method, 10);
        item.Route = item.Route is null ? null : Text(item.Route, 240);
        item.ExternalService = item.ExternalService is null ? null : Text(item.ExternalService, 32);
        item.ErrorCode = item.ErrorCode is null ? null : Text(item.ErrorCode, 80);
        item.ExceptionType = item.ExceptionType is null ? null : Text(item.ExceptionType, 180);
        item.ExceptionDetail = item.ExceptionDetail is null
            ? null
            : string.Join('\n', item.ExceptionDetail[..Math.Min(item.ExceptionDetail.Length, 12000)].Split('\n').Take(200).Select(line => Text(line, 320)));
        if (item.DurationMs is { } duration && (!double.IsFinite(duration) || duration < 0))
        {
            item.DurationMs = null;
        }
    }
    private static string? Hex(string? value, int length) => value?.Length == length && value.All(char.IsAsciiHexDigit) ? value.ToLowerInvariant() : null;
    [GeneratedRegex("(?i)(password|pwd|token|api[_-]?key|authorization|cookie|secret|connectionstring|user id|uid|server|data source)\\s*[=:]\\s*(?:\"[^\"]*\"|'[^']*'|[^;,\\s]+)", RegexOptions.CultureInvariant)] private static partial Regex Secret();
    [GeneratedRegex(@"(?i)\b(?:bearer\s+\S+|AIza[\w-]+|sk-[\w-]{8,}|eyJ[\w-]+\.[\w-]+\.[\w-]+)", RegexOptions.CultureInvariant)] private static partial Regex Bearer();
    [GeneratedRegex(@"(?i)\b(?:https?|ftp)://[^\s<>""']+", RegexOptions.CultureInvariant)] private static partial Regex Url();
    [GeneratedRegex(@"(?:[A-Za-z]:\\|\\\\)[^\s<>""']+|/(?:home|var|usr|etc|opt|srv|tmp)/[^\s<>""']+", RegexOptions.CultureInvariant)] private static partial Regex PathPattern();
    [GeneratedRegex(@"\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b", RegexOptions.CultureInvariant)] private static partial Regex Email();
}
