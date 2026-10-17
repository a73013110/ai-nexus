using System.Globalization;
using System.Text;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>一段 migration 範圍的發布 SQL，準備寫入 schema/、data/ 與 manifest.md。</summary>
/// <param name="FromMigration">正式資料庫目前最後套用的 migration（不含）；空庫為 <see langword="null"/>。</param>
/// <param name="ToMigration">範圍內最後一個 migration；組件中沒有任何 migration 時為 <see langword="null"/>。</param>
/// <param name="Migrations">範圍內的 migration，依順序排列。</param>
/// <param name="Files">所有檔案，依執行順序排列。</param>
public sealed record ReleaseScript(string? FromMigration, string? ToMigration, IReadOnlyList<string> Migrations, IReadOnlyList<ReleaseScriptFile> Files)
{
    public const string ManifestFileName = "manifest.md";

    // SSMS 與 sqlcmd -f 65001 只有在檔案帶 BOM 時才能穩定讀成 UTF-8；沒有 BOM 時 sqlcmd 會退回 ANSI 碼頁，中文欄位說明會亂碼。
    private static readonly UTF8Encoding SqlEncoding = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>把所有檔案與清單寫入 <paramref name="directory"/>；目錄必須不存在或為空，避免舊檔混入。</summary>
    public void WriteTo(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new ReleaseScriptException($"輸出目錄必須不存在或是空的，避免混入舊檔：{root}");
        Directory.CreateDirectory(Path.Combine(root, "schema"));
        Directory.CreateDirectory(Path.Combine(root, "data"));
        foreach (var file in Files) File.WriteAllText(Path.Combine(root, file.Path), file.Header + file.Sql, SqlEncoding);
        File.WriteAllText(Path.Combine(root, ManifestFileName), Manifest(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>DBA 執行時依循的清單，依執行順序列出所有檔案。</summary>
    public string Manifest()
    {
        var text = new StringBuilder();
        text.AppendLine("# 發布 SQL 執行清單").AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"- 範圍：`{FromMigration ?? "0"}`{(FromMigration is null ? "（空庫）" : "")} 之後到 `{ToMigration ?? "0"}`，共 {Migrations.Count} 個 migration。");
        text.AppendLine("- 依「編號」由小到大逐檔執行；`schema/`（結構）與 `data/`（資料）交錯，不能只跑其中一個資料夾。");
        text.AppendLine("- 每個檔都先查 `__EFMigrationsHistory`，所屬 migration 已套用就整檔略過。每個 migration 的最後一個檔寫入 history，必須等該 migration 其他檔都成功才執行。");
        text.AppendLine("- 一般檔自帶 `BEGIN TRANSACTION`／`COMMIT`；注意欄標「交易外」的檔含不能在交易內執行的語句，不要再包交易。");
        text.AppendLine("- 檔案為 UTF-8（含 BOM），批次以 `GO` 分隔；sqlcmd 請加 `-b -I -f 65001`（`-I` 開啟 QUOTED_IDENTIFIER，filtered index 需要）。").AppendLine();
        text.AppendLine("| 編號 | 類型 | 物件 | Migration | 檔案 | 注意 |");
        text.AppendLine("| ---: | --- | --- | --- | --- | --- |");
        foreach (var file in Files)
        {
            var notes = new List<string>();
            if (file.OutsideTransaction) notes.Add("交易外");
            if (file.Operations.Count == 0 && file.MigrationId is not null) notes.Add("寫入 history（本 migration 最後一檔）");
            var note = string.Join("；", notes);
            text.AppendLine(CultureInfo.InvariantCulture, $"| {file.Label} | {file.Folder} | `{file.Target}` | {file.MigrationId ?? "共用"} | `{file.Path}` | {note} |");
        }
        return text.ToString();
    }
}
