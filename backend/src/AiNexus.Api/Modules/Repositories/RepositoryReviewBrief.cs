using System.Text;
using System.Text.Json;

namespace AiNexus.Modules.Repositories;

/// <summary>A small provider-independent contract keeps final reports complete, localized and bounded.</summary>
public static class RepositoryReviewBrief
{
    public const string Instruction = "只輸出一個完整 JSON 物件，不加 Markdown 圍欄或其他文字：" +
        "{\"conclusion\":\"一句話結論\",\"changes\":[\"變更重點\"],\"findings\":[{\"priority\":\"P1或P2\",\"location\":\"檔案:行號\",\"detail\":\"觸發條件、影響與修正方向\"}],\"limitation\":\"確實存在的限制，沒有則空字串\"}。" +
        "conclusion 最多 80 字；changes 至多 3 項、每項最多 60 字；findings 至多 3 項、detail 每項最多 80 字、location 最多 180 字元；limitation 最多 60 字。" +
        "說明文字合計最多 350 字（不含 location），不得換行或貼程式碼。合併重複問題；沒有明確問題則 findings 為空陣列，結論說明未發現明確缺陷。summary 模式 findings 必須為空陣列。每個說明欄位都要使用繁體中文。";

    public static bool TryRender(string json, string purpose, out string markdown)
    {
        markdown = "";
        if (json.Length > 6000) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var conclusion = Text(root.GetProperty("conclusion"), 80);
            var changes = root.GetProperty("changes"); var findings = root.GetProperty("findings");
            var limitation = Text(root.GetProperty("limitation"), 60, optional: true);
            if (changes.ValueKind != JsonValueKind.Array || changes.GetArrayLength() > 3 ||
                findings.ValueKind != JsonValueKind.Array || findings.GetArrayLength() > 3 ||
                (purpose == "summary" && findings.GetArrayLength() != 0)) return false;
            var length = conclusion.Length + limitation.Length;
            var output = new StringBuilder(conclusion);
            if (changes.GetArrayLength() > 0) output.Append("\n\n**變更重點**");
            foreach (var change in changes.EnumerateArray())
            {
                var text = Text(change, 60); length += text.Length;
                output.Append("\n- ").Append(text);
            }
            if (findings.GetArrayLength() > 0) output.Append("\n\n**優先確認**");
            foreach (var finding in findings.EnumerateArray())
            {
                var priority = finding.GetProperty("priority").GetString();
                if (priority is not ("P1" or "P2")) return false;
                var location = Text(finding.GetProperty("location"), 180, localized: false);
                if (location.Contains('`')) return false;
                var detail = Text(finding.GetProperty("detail"), 80); length += detail.Length;
                output.Append("\n- **").Append(priority).Append("** `").Append(location).Append("`：").Append(detail);
            }
            if (length > 350) return false;
            if (limitation.Length > 0) output.Append("\n\n限制：").Append(limitation);
            markdown = output.ToString(); return true;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return false;
        }
    }

    private static string Text(JsonElement element, int limit, bool optional = false, bool localized = true)
    {
        var text = element.GetString()?.Trim() ?? throw new FormatException();
        if ((text.Length == 0 && !optional) || text.Length > limit || text.Any(char.IsControl) || text.Contains("```") ||
            (localized && text.Length > 0 && !text.Any(c => c is >= '\u3400' and <= '\u9fff')))
            throw new FormatException();
        return text;
    }
}
