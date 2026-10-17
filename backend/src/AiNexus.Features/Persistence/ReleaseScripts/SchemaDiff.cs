using System.Globalization;
using System.Text;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>一次發布在各結構物件上的定義變化：正式版與發布版的結構，各自由 SqlPackage 逐物件匯出後比對。</summary>
/// <param name="FromMigration">正式資料庫目前最後套用的 migration；空庫為 <see langword="null"/>。</param>
/// <param name="ToMigration">發布的最後一個 migration。</param>
/// <param name="Migrations">兩者之間的 migration，依順序排列。</param>
/// <param name="ServerVersion">參考 SQL Server 的產品版本。</param>
/// <param name="FullTextAvailable">參考伺服器是否有全文元件與繁中斷詞器；有的話 <c>InitialCreate</c> 會建立知識全文索引。</param>
/// <param name="Changes">有變化的物件，依路徑排序。</param>
public sealed record SchemaDiff(
    string? FromMigration,
    string ToMigration,
    IReadOnlyList<string> Migrations,
    string ServerVersion,
    bool FullTextAvailable,
    IReadOnlyList<SchemaObjectChange> Changes)
{
    public const string ManifestFileName = "manifest.md";

    // 與發布 SQL 相同：要帶 BOM，記事本與 SSMS 才能正確顯示差異中的中文說明。
    private static readonly UTF8Encoding DiffEncoding = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>逐檔比對兩個 SqlPackage <c>ExtractTarget=SchemaObjectType</c> 匯出資料夾。</summary>
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

    /// <summary>把差異檔與清單寫入 <paramref name="directory"/>；目錄必須不存在或為空。</summary>
    public void WriteTo(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new ReleaseScriptException($"輸出目錄必須不存在或是空的，避免混入舊檔：{root}");
        Directory.CreateDirectory(root);
        foreach (var change in Changes) File.WriteAllText(Path.Combine(root, change.FileName), change.Diff, DiffEncoding);
        File.WriteAllText(Path.Combine(root, ManifestFileName), Manifest(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>DBA 要審查的清單：每個新增、刪除與修改的物件，附上差異檔。</summary>
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
    /// 把物件腳本的 <c>GO</c> 批次排成固定順序，並合併對齊用的空白（<see cref="Collapse"/>）。
    /// SqlPackage 每次匯出時，物件本體之後的批次（索引、全文索引、<c>MS_Description</c>）順序都不固定，
    /// 不排序的話每張表都會被誤判為有變動。排序後物件本體排在最前，接著是索引與其他語句、全文索引、說明；
    /// 同一類內保留原本的先後順序。
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
    /// 縮排之後、字串常值之外的連續空白合併為一個空白。SqlPackage 會把資料表的欄位型別對齊，
    /// 若不合併，新增或刪除一個較長的欄位名稱就會讓每一行欄位都顯示為變動。
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
            .ToDictionary(
                file => Path.GetRelativePath(root, file).Replace('\\', '/'),
                file => Normalize(File.ReadAllText(file)),
                StringComparer.OrdinalIgnoreCase);
    }
}
