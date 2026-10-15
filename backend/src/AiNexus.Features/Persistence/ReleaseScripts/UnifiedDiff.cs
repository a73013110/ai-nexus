using System.Globalization;
using System.Text;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>
/// Line diff in the unified format of <c>diff -u</c> and <c>git diff</c>, so a DBA reads it in any diff viewer. Object
/// scripts are a few hundred lines, so the longest common subsequence is computed exactly between the first and last
/// differing lines; a pair too large for that is shown as one replaced block instead.
/// </summary>
public static class UnifiedDiff
{
    public const int ContextLines = 3;

    /// <summary>Label of the missing side of an added or removed object, as in <c>diff -u</c>.</summary>
    public const string Missing = "/dev/null";

    // Cells of the LCS table (about 100 MB of ints); far beyond any object script.
    private const long MaxTableCells = 25_000_000;

    /// <returns>The diff with <c>---</c>/<c>+++</c> headers, or an empty string when the lines are equal.</returns>
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
            // Changes closer than two contexts apart share a hunk, so no line is printed twice.
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

    /// <summary>Lines of a text file, whatever its line endings; a final line break does not add an empty line.</summary>
    public static IReadOnlyList<string> Lines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    /// <summary><c>start,count</c> with 1-based lines; an empty range names the line before it, and a count of 1 is omitted.</summary>
    private static string Range(int lineBefore, int count)
    {
        var start = count == 0 ? lineBefore : lineBefore + 1;
        return count == 1 ? start.ToString(CultureInfo.InvariantCulture) : string.Create(CultureInfo.InvariantCulture, $"{start},{count}");
    }

    /// <summary>Every line in order: kept (' '), removed ('-') or added ('+'), with the lines of each side before it.</summary>
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
            // common[i, j]: length of the longest common subsequence of removed[i..] and added[j..].
            var common = new int[removed.Count + 1, added.Count + 1];
            for (var i = removed.Count - 1; i >= 0; i--)
                for (var j = added.Count - 1; j >= 0; j--)
                    common[i, j] = removed[i] == added[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);
            int r = 0, a = 0;
            while (r < removed.Count && a < added.Count)
            {
                if (removed[r] == added[a]) { Add(' ', removed[r]); r++; a++; }
                // Removals first on a tie, so a changed line reads as "- old" then "+ new".
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
