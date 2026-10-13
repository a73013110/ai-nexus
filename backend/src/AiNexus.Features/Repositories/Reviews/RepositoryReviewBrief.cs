using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiNexus.Features.Repositories.Reviews;

/// <summary>A small provider-independent contract keeps final reports complete, localized and bounded.</summary>
/// <remarks>
/// Local models overshoot character limits or wrap JSON in fences even when they follow the structure. Mild deviations are
/// normalized (fences, line breaks, extra items, text up to twice its limit is shortened); only output that cannot be rendered
/// faithfully is rejected, and the rejection carries a stable reason for the log.
/// </remarks>
public static partial class RepositoryReviewBrief
{
    public const string Instruction = "只輸出一個完整 JSON 物件，不加 Markdown 圍欄或其他文字：" +
        "{\"conclusion\":\"一句話結論\",\"changes\":[\"變更重點\"],\"findings\":[{\"priority\":\"P1或P2\",\"location\":\"檔案:行號\",\"detail\":\"觸發條件、影響與修正方向\"}],\"limitation\":\"確實存在的限制，沒有則空字串\"}。" +
        "conclusion 最多 80 字；changes 至多 3 項、每項最多 60 字；findings 至多 3 項、detail 每項最多 80 字、location 最多 180 字元；limitation 最多 60 字。" +
        "說明文字合計最多 350 字（不含 location），不得換行或貼程式碼。合併重複問題；沒有明確問題則 findings 為空陣列，結論說明未發現明確缺陷。summary 模式 findings 必須為空陣列。每個說明欄位都要使用繁體中文。";

    private const int MaxItems = 3;

    public static bool TryRender(string json, string purpose, out string markdown) => TryRender(json, purpose, out markdown, out _);

    /// <summary>Renders the brief, or returns a stable, content-free <paramref name="reason"/> such as <c>json</c> or <c>conclusion.length</c>.</summary>
    public static bool TryRender(string json, string purpose, out string markdown, out string reason)
    {
        markdown = ""; reason = "";
        try
        {
            var root = Parse(json);
            if (root.ValueKind != JsonValueKind.Object) throw new BriefException("json.shape");
            var conclusion = Field(root, "conclusion", 80) ?? throw new BriefException("conclusion.missing");
            var limitation = Field(root, "limitation", 60, optional: true) ?? "";
            var changes = Items(root, "changes").Select(x => Text(x, "changes", 60)).OfType<string>().Take(MaxItems).ToArray();
            // A summary has no findings by contract; dropping them keeps the summary instead of failing the whole report.
            var findings = purpose == "summary" ? [] : Items(root, "findings").Take(MaxItems).Select(Finding).ToArray();

            var output = new StringBuilder(conclusion);
            if (changes.Length > 0) output.Append("\n\n**變更重點**");
            foreach (var change in changes) output.Append("\n- ").Append(change);
            if (findings.Length > 0) output.Append("\n\n**優先確認**");
            foreach (var (priority, location, detail) in findings)
                output.Append("\n- **").Append(priority).Append("** `").Append(location).Append("`：").Append(detail);
            if (limitation.Length > 0) output.Append("\n\n限制：").Append(limitation);
            markdown = output.ToString(); return true;
        }
        catch (BriefException e) { reason = e.Reason; return false; }
        catch (JsonException) { reason = "json"; return false; }
    }

    private static JsonElement Parse(string text)
    {
        if (text.Length > 6000) throw new BriefException("length");
        text = Think().Replace(text, "");
        // Tolerate fences or a sentence around the object; the object itself must still be complete JSON.
        var start = text.IndexOf('{'); var end = text.LastIndexOf('}');
        if (start < 0 || end < start) throw new BriefException("json");
        using var document = JsonDocument.Parse(text[start..(end + 1)], new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        return document.RootElement.Clone();
    }

    private static IEnumerable<JsonElement> Items(JsonElement root, string name) =>
        !root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null ? []
        : value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : throw new BriefException($"{name}.shape");

    private static (string Priority, string Location, string Detail) Finding(JsonElement finding)
    {
        if (finding.ValueKind != JsonValueKind.Object) throw new BriefException("findings.shape");
        var priority = Field(finding, "priority", 4, localized: false)?.ToUpperInvariant();
        if (priority is not ("P1" or "P2")) throw new BriefException("findings.priority");
        var location = (Field(finding, "location", 180, localized: false, optional: true) ?? "").Replace('`', '\'');
        var detail = Field(finding, "detail", 80) ?? throw new BriefException("findings.detail.missing");
        return (priority, location.Length > 0 ? location : "未標示位置", detail);
    }

    private static string? Field(JsonElement owner, string name, int limit, bool optional = false, bool localized = true)
    {
        if (!owner.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return optional ? null : throw new BriefException($"{name}.missing");
        return Text(value, name, limit, optional, localized);
    }

    private static string? Text(JsonElement value, string name, int limit, bool optional = false, bool localized = true)
    {
        if (value.ValueKind != JsonValueKind.String) throw new BriefException($"{name}.shape");
        var text = Whitespace().Replace(value.GetString()!, " ").Trim();
        if (text.Length == 0) return optional ? "" : null;
        if (text.Contains("```")) throw new BriefException($"{name}.code");
        if (localized && !text.Any(c => c is >= '㐀' and <= '鿿')) throw new BriefException($"{name}.language");
        // Twice the limit means the model ignored the brief format; a correction is cheaper than a mangled sentence.
        if (text.Length > limit * 2) throw new BriefException($"{name}.length");
        return text.Length <= limit ? text : string.Concat(text.AsSpan(0, limit - 1), "…");
    }

    private sealed class BriefException(string reason) : Exception(reason)
    {
        public string Reason { get; } = reason;
    }

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline)]
    private static partial Regex Think();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
