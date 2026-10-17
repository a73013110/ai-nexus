namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>兩份 SqlPackage 逐物件匯出結果之間，定義有差異的一個結構物件。</summary>
/// <param name="Status">此次發布對該物件的變化：新增、移除或修改。</param>
/// <param name="Path">物件腳本在匯出目錄中的路徑，使用斜線，例如 <c>knowledge/Tables/Chunks.sql</c>。</param>
/// <param name="Type">SqlPackage 對該物件種類的資料夾名稱，例如 <c>Tables</c>、<c>Views</c>、<c>Security</c>（schema）。</param>
/// <param name="Name">物件名稱；屬於某個 schema 的物件為 <c>schema.Name</c>。</param>
/// <param name="Diff">物件腳本由舊到新的 unified diff。</param>
public sealed record SchemaObjectChange(SchemaObjectStatus Status, string Path, string Type, string Name, string Diff)
{
    /// <summary>差異檔名：把路徑的資料夾以點連接，例如 <c>knowledge.Tables.Chunks.diff</c>。</summary>
    public string FileName => System.IO.Path.ChangeExtension(Path, null).Replace('/', '.') + ".diff";
}
