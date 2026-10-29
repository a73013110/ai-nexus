using System.Globalization;
using System.Text;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Export;

namespace AiNexus.Features.Admin.Releases.SchemaChanges;

/// <summary>
/// 異動物件目前的定義，存成 <c>.sql</c> 套用到開發庫後以 <c>Export-Database.ps1</c> 匯出提交（ADR 0028：客戶端熱修的回收）。
/// 程式物件與 synonym 是可執行的 DROP＋CREATE；資料表沒有可靠的 DDL 可還原，只附上目前的欄位、索引與約束，由工程師照著改。
/// </summary>
internal sealed class ExportSchemaChanges(SchemaChangeReader changes, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/export", (ExportSchemaChanges handler, CancellationToken ct) => handler.HandleAsync(ct).ToHttpResultAsync(file => file.ToHttpResult()))
        .RequireRateLimiting(ReleasesModule.ExportRateLimit)
        .Audited(ReleasesAudit.SchemaChangesExported)
        .WithName("ExportSchemaChanges");

    public async Task<Result<ExportFile>> HandleAsync(CancellationToken ct)
    {
        var list = await changes.ListAsync(ct);
        var definitions = (await changes.DefinitionsAsync(list.Where(x => x.Change != "removed"), ct))
            .ToDictionary(x => (x.SchemaName, x.Name));
        var text = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"-- 與這一版應有結構不同的物件，產生於 {clock.GetUtcNow():yyyy-MM-dd HH:mm} UTC，共 {list.Count} 個。")
            .AppendLine("-- 在開發庫執行（一般模式）後以 Export-Database.ps1 匯出提交；先確認不會覆蓋開發庫上尚未發版的修改。")
            .AppendLine("-- 程式物件以 DROP＋CREATE 重建，被 SCHEMABINDING 引用時要先處理引用它的物件。")
            .AppendLine();
        foreach (var change in list.OrderBy(x => x.SchemaName, StringComparer.Ordinal).ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            var name = $"[{change.SchemaName.Replace("]", "]]", StringComparison.Ordinal)}].[{change.Name.Replace("]", "]]", StringComparison.Ordinal)}]";
            text.AppendLine(CultureInfo.InvariantCulture, $"-- {change.Change} {change.Type} {name}");
            if (!definitions.TryGetValue((change.SchemaName, change.Name), out var current))
            {
                text.AppendLine("-- 目前的資料庫沒有這個物件；開發庫若仍需要，以 repo 的定義為準。").AppendLine();
                continue;
            }
            switch (current.Type)
            {
                case "procedure" or "function" or "view" or "trigger":
                    text.AppendLine(CultureInfo.InvariantCulture, $"DROP {current.Type.ToUpperInvariant()} IF EXISTS {name};").AppendLine("GO")
                        .AppendLine(current.Definition.TrimEnd()).AppendLine("GO");
                    break;
                case "synonym":
                    text.AppendLine(CultureInfo.InvariantCulture, $"DROP SYNONYM IF EXISTS {name};").AppendLine("GO")
                        .AppendLine(CultureInfo.InvariantCulture, $"CREATE {current.Definition.Replace("SYNONYM FOR", $"SYNONYM {name} FOR", StringComparison.Ordinal)};").AppendLine("GO");
                    break;
                default:
                    text.AppendLine("/* 資料表目前的定義（不是可執行的 DDL）：");
                    text.AppendLine(current.Definition.Replace("*/", "* /", StringComparison.Ordinal).TrimEnd()).AppendLine("*/");
                    break;
            }
            text.AppendLine();
        }
        // UTF-8 加 BOM：SSMS 開啟沒有 BOM 的檔案時會用系統字碼頁，中文註解與字串會變亂碼。
        return new ExportFile("ai-nexus-schema-changes.sql", "application/sql", [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())]);
    }
}
