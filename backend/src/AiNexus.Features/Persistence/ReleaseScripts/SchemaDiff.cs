using System.Globalization;
using System.Text;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>
/// Per-object definition changes of a release: the production version's schema against the release version's, both
/// extracted by SqlPackage one file per object. Written as <c>diff/</c> with one unified diff per changed object and
/// <see cref="ManifestFileName"/> listing them.
/// </summary>
/// <param name="FromMigration">The production database's last migration; <see langword="null"/> for an empty database.</param>
/// <param name="ToMigration">The release's last migration.</param>
/// <param name="Migrations">The migrations between them, in order.</param>
/// <param name="ServerVersion">The reference SQL Server's product version.</param>
/// <param name="FullTextAvailable">The reference server has full-text search with the Traditional Chinese word breaker, so <c>InitialCreate</c> created the knowledge full-text index.</param>
/// <param name="Changes">The changed objects, ordered by path.</param>
public sealed record SchemaDiff(
    string? FromMigration,
    string ToMigration,
    IReadOnlyList<string> Migrations,
    string ServerVersion,
    bool FullTextAvailable,
    IReadOnlyList<SchemaObjectChange> Changes)
{
    public const string ManifestFileName = "manifest.md";

    // Same as the release SQL: Notepad and SSMS read the Chinese column descriptions in the diffs reliably with a BOM.
    private static readonly UTF8Encoding DiffEncoding = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Compares two SqlPackage <c>ExtractTarget=SchemaObjectType</c> folders file by file.</summary>
    public static IReadOnlyList<SchemaObjectChange> Compare(string before, string after)
    {
        var old = Scripts(before);
        var current = Scripts(after);
        var changes = new List<SchemaObjectChange>();
        foreach (var path in old.Keys.Union(current.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            var oldText = old.GetValueOrDefault(path);
            var newText = current.GetValueOrDefault(path);
            var status = oldText is null ? SchemaObjectStatus.Added : newText is null ? SchemaObjectStatus.Removed : SchemaObjectStatus.Modified;
            var diff = UnifiedDiff.Create(
                oldText is null ? UnifiedDiff.Missing : "before/" + path,
                newText is null ? UnifiedDiff.Missing : "after/" + path,
                UnifiedDiff.Lines(oldText ?? ""), UnifiedDiff.Lines(newText ?? ""));
            if (diff.Length == 0) continue;
            var parts = path.Split('/');
            var name = Path.GetFileNameWithoutExtension(parts[^1]);
            changes.Add(new(status, path, parts.Length > 1 ? parts[^2] : "", parts.Length > 2 ? parts[0] + "." + name : name, diff));
        }
        return changes;
    }

    /// <summary>Writes the diffs and the manifest into <paramref name="directory"/>, which must be missing or empty.</summary>
    public void WriteTo(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new ReleaseScriptException($"輸出目錄必須不存在或是空的，避免混入舊檔：{root}");
        Directory.CreateDirectory(root);
        foreach (var change in Changes) File.WriteAllText(Path.Combine(root, change.FileName), change.Diff, DiffEncoding);
        File.WriteAllText(Path.Combine(root, ManifestFileName), Manifest(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>The list the DBA reviews: every added, removed and modified object with its diff file.</summary>
    public string Manifest()
    {
        var text = new StringBuilder();
        text.AppendLine("# 結構差異清單").AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"- 範圍：`{FromMigration ?? "0"}`{(FromMigration is null ? "（空庫）" : "")} → `{ToMigration}`，共 {Migrations.Count} 個 migration。");
        text.AppendLine(CultureInfo.InvariantCulture, $"- 做法：在參考 SQL Server（{ServerVersion}）建暫存庫，依序套用 migration 到兩端版本，各以 SqlPackage 逐物件匯出後比對；暫存庫已刪除。只比結構，不含資料。");
        text.AppendLine("- 比對前先整理 SqlPackage 的輸出：同一物件內的批次（每次匯出順序不固定）依「物件本體、索引、全文索引、說明」排序，字串以外的連續空白合為一個（欄位對齊）。差異只反映定義變動，不是可執行的升級 SQL。");
        text.AppendLine(FullTextAvailable
            ? "- 參考伺服器有全文元件與繁中斷詞器，所以結構含 `KnowledgeSearch` 全文目錄與 `knowledge.Chunks` 的全文索引。"
            : "- 參考伺服器缺全文元件或繁中斷詞器，`InitialCreate` 略過了全文索引，所以這裡與 `expected.dacpac` 都不含 `KnowledgeSearch`；正式機若有全文元件，會多出它們。");
        text.AppendLine(CultureInfo.InvariantCulture, $"- 新增 {Count(SchemaObjectStatus.Added)}、刪除 {Count(SchemaObjectStatus.Removed)}、修改 {Count(SchemaObjectStatus.Modified)} 個物件。").AppendLine();
        if (Changes.Count == 0) return text.AppendLine("這個範圍沒有結構變更。").ToString();
        text.AppendLine("| 狀態 | 類型 | 物件 | 差異檔 |");
        text.AppendLine("| --- | --- | --- | --- |");
        foreach (var change in Changes)
            text.AppendLine(CultureInfo.InvariantCulture, $"| {Label(change.Status)} | {change.Type} | `{change.Name}` | [`{change.FileName}`]({change.FileName}) |");
        return text.ToString();
    }

    /// <summary>
    /// The object script with its <c>GO</c> batches in a fixed order and its alignment collapsed (<see cref="Collapse"/>). SqlPackage writes the batches after the object's
    /// own CREATE (indexes, the full-text index, <c>MS_Description</c> properties) in a different order on every extract, so
    /// unsorted they would show every table as changed. The order kept is the readable one: the object, its indexes and
    /// other statements, its full-text index, its descriptions; text order within each.
    /// </summary>
    public static string Normalize(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var batches = new List<string>();
        var current = new List<string>();
        void Flush()
        {
            var text = string.Join('\n', current).Trim('\n', ' ', '\t');
            if (text.Length > 0) batches.Add(text);
            current.Clear();
        }
        foreach (var line in UnifiedDiff.Lines(script))
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase)) Flush();
            else current.Add(Collapse(line.TrimEnd()));
        }
        Flush();
        var ordered = batches.Take(1).Concat(batches.Skip(1).OrderBy(Rank).ThenBy(x => x, StringComparer.Ordinal));
        return string.Concat(ordered.Select(batch => batch + "\n\n\nGO\n\n"));
    }

    /// <summary>
    /// One space for each run of white space after the indentation and outside string literals. SqlPackage aligns the
    /// column types of a table, so adding or dropping a long column name would otherwise change every column line.
    /// </summary>
    private static string Collapse(string line)
    {
        var text = new StringBuilder(line.Length);
        var indent = line.Length - line.TrimStart().Length;
        text.Append(line, 0, indent);
        var literal = false;
        for (var i = indent; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '\'') literal = !literal;
            if (!literal && char.IsWhiteSpace(c) && text.Length > indent && char.IsWhiteSpace(text[^1])) continue;
            text.Append(!literal && char.IsWhiteSpace(c) ? ' ' : c);
        }
        return text.ToString();
    }

    private static int Rank(string batch)
        => batch.StartsWith("EXECUTE sp_addextendedproperty", StringComparison.OrdinalIgnoreCase) ? 3
            : batch.StartsWith("CREATE FULLTEXT", StringComparison.OrdinalIgnoreCase) ? 2
            : 1;

    private int Count(SchemaObjectStatus status) => Changes.Count(x => x.Status == status);

    private static string Label(SchemaObjectStatus status) => status switch
    {
        SchemaObjectStatus.Added => "新增",
        SchemaObjectStatus.Removed => "刪除",
        _ => "修改",
    };

    private static Dictionary<string, string> Scripts(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root)) return new(StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(root, "*.sql", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(root, file).Replace('\\', '/'), file => Normalize(File.ReadAllText(file)), StringComparer.OrdinalIgnoreCase);
    }
}
