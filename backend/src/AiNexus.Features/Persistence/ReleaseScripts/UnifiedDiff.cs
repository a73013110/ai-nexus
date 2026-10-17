using System.Globalization;
using System.Text;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>
/// 採用 <c>diff -u</c> 與 <c>git diff</c> 的 unified 格式，DBA 用任何 diff 工具都能看。
/// 物件腳本通常只有幾百行，因此在第一行與最後一行差異之間精確計算最長共同子序列（LCS）；
/// 若表格太大，則改為把整段視為一個取代區塊，不做精確比對。
/// </summary>
public static class UnifiedDiff
{
    public const int ContextLines = 3;

    /// <summary>新增或刪除物件時，缺少的那一側所用的標籤，與 <c>diff -u</c> 相同。</summary>
    public const string Missing = "/dev/null";

    // LCS 表格的格子上限（約 100 MB 的 int），遠大於任何物件腳本；超過時改用整段取代，避免記憶體暴增。
    private const long MaxTableCells = 25_000_000;

    /// <returns>含 <c>---</c>／<c>+++</c> 標頭的差異；內容相同時回傳空字串。</returns>
    public static string Create(string oldLabel, string newLabel, IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines, int context = ContextLines)
    {
        ArgumentNullException.ThrowIfNull(oldLines);
        ArgumentNullException.ThrowIfNull(newLines);
        ArgumentOutOfRangeException.ThrowIfNegative(context);
        var edits = Edits(oldLines, newLines);
        var changes = Enumerable.Range(0, edits.Count).Where(i => edits[i].Kind != ' ').ToList();
        if (changes.Count == 0) return "";

        var text = new StringBuilder();
        text.AppendLine("--- " + oldLabel).AppendLine("+++ " + newLabel);
        for (var first = 0; first < changes.Count;)
        {
            // 距離不到兩段 context 的變更併入同一個 hunk，避免同一行被印兩次。
            var last = first;
            while (last + 1 < changes.Count && changes[last + 1] - changes[last] <= 2 * context + 1) last++;
            var start = Math.Max(0, changes[first] - context);
            var end = Math.Min(edits.Count, changes[last] + context + 1);
            var hunk = edits.Skip(start).Take(end - start).ToList();
            text.Append("@@ -").Append(Range(edits[start].OldLine, hunk.Count(x => x.Kind != '+')))
                .Append(" +").Append(Range(edits[start].NewLine, hunk.Count(x => x.Kind != '-'))).AppendLine(" @@");
            foreach (var edit in hunk) text.Append(edit.Kind).AppendLine(edit.Text);
            first = last + 1;
        }
        return text.ToString();
    }

    /// <summary>文字檔的各行，不論換行符號為何；結尾的換行不會多出一個空行。</summary>
    public static IReadOnlyList<string> Lines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    /// <summary>產生 <c>start,count</c>（行號從 1 開始）；空範圍指向它前面那一行，數量為 1 時省略。</summary>
    private static string Range(int lineBefore, int count)
    {
        var start = count == 0 ? lineBefore : lineBefore + 1;
        return count == 1 ? start.ToString(CultureInfo.InvariantCulture) : string.Create(CultureInfo.InvariantCulture, $"{start},{count}");
    }

    /// <summary>依順序列出每一行：保留（' '）、刪除（'-'）或新增（'+'），並記錄兩側各自的行號。</summary>
    private static List<Edit> Edits(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var prefix = 0;
        while (prefix < before.Count && prefix < after.Count && before[prefix] == after[prefix]) prefix++;
        var suffix = 0;
        while (suffix < before.Count - prefix && suffix < after.Count - prefix && before[^(suffix + 1)] == after[^(suffix + 1)]) suffix++;
        var removed = before.Skip(prefix).Take(before.Count - prefix - suffix).ToList();
        var added = after.Skip(prefix).Take(after.Count - prefix - suffix).ToList();

        var edits = new List<Edit>(before.Count + added.Count);
        int oldLine = 0, newLine = 0;
        void Add(char kind, string text)
        {
            edits.Add(new(kind, text, oldLine, newLine));
            if (kind != '+') oldLine++;
            if (kind != '-') newLine++;
        }

        foreach (var line in before.Take(prefix)) Add(' ', line);
        if ((long)(removed.Count + 1) * (added.Count + 1) > MaxTableCells)
        {
            foreach (var line in removed) Add('-', line);
            foreach (var line in added) Add('+', line);
        }
        else
        {
            // common[i, j]：removed[i..] 與 added[j..] 的最長共同子序列長度。
            var common = new int[removed.Count + 1, added.Count + 1];
            for (var i = removed.Count - 1; i >= 0; i--)
                for (var j = added.Count - 1; j >= 0; j--)
                    common[i, j] = removed[i] == added[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);
            int r = 0, a = 0;
            while (r < removed.Count && a < added.Count)
            {
                if (removed[r] == added[a]) { Add(' ', removed[r]); r++; a++; }
                // 長度相同時先輸出刪除，讓修改的行顯示為「- 舊」接「+ 新」。
                else if (common[r + 1, a] >= common[r, a + 1]) Add('-', removed[r++]);
                else Add('+', added[a++]);
            }
            while (r < removed.Count) Add('-', removed[r++]);
            while (a < added.Count) Add('+', added[a++]);
        }
        foreach (var line in before.Skip(before.Count - suffix)) Add(' ', line);
        return edits;
    }

    private sealed record Edit(char Kind, string Text, int OldLine, int NewLine);
}
