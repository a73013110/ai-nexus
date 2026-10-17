using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>一個有編號的發布 SQL 檔。</summary>
/// <param name="Number">執行順序。schema/ 與 data/ 共用同一套序號，序號之間以 <see cref="ReleaseScriptGenerator.NumberStep"/> 間隔。</param>
/// <param name="Kind">所屬資料夾，也決定由哪一方 DBA 執行。</param>
/// <param name="Target">變動的物件：<c>schema.Table</c>、schema 名稱或操作名稱。</param>
/// <param name="MigrationId">所屬的 migration；共用的 history 表檔案為 <see langword="null"/>。</param>
/// <param name="OutsideTransaction">含有 SQL Server 不能在交易內執行的語句。</param>
/// <param name="Path">使用斜線的相對路徑，例如 <c>schema/0420_knowledge.Chunks.sql</c>。</param>
/// <param name="Sql">EF 產生的批次內容，不含檔頭註解。</param>
/// <param name="Operations">產生此檔所依據的 migration 操作；寫入 history 的檔案為空。</param>
public sealed record ReleaseScriptFile(
    int Number,
    ReleaseScriptKind Kind,
    string Target,
    string? MigrationId,
    bool OutsideTransaction,
    string Path,
    string Sql,
    IReadOnlyList<MigrationOperation> Operations)
{
    public string Folder => Kind == ReleaseScriptKind.Data ? "data" : "schema";

    /// <summary>檔名中的補零序號。</summary>
    public string Label => Path[(Folder.Length + 1)..Path.IndexOf('_', StringComparison.Ordinal)];

    /// <summary>寫在 <see cref="Sql"/> 上方的註解行。</summary>
    public string Header
    {
        get
        {
            var header = $"-- {Label}｜{Folder}｜{Target}｜{MigrationId ?? "共用"}{Environment.NewLine}";
            return OutsideTransaction ? header + "-- 交易外執行：含不能在交易內執行的語句，不要包進 BEGIN TRANSACTION。" + Environment.NewLine : header;
        }
    }
}
