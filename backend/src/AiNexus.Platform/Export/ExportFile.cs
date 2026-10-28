using Microsoft.AspNetCore.Http.HttpResults;

namespace AiNexus.Platform.Export;

/// <summary>
/// 一個要下載的匯出檔：檔名、Content-Type 與內容，各格式（<see cref="Csv"/>、<see cref="Spreadsheet"/>、<see cref="MarkdownDocument"/>、<see cref="PdfRenderer"/>）共用。
/// /api 回應一律不快取（<c>WebSecurity.NoStore</c> 在送出前覆寫），端點不必自己設 Cache-Control。
/// </summary>
public sealed record ExportFile(string Name, string ContentType, byte[] Content)
{
    public FileContentHttpResult ToHttpResult() => TypedResults.File(Content, ContentType, Name);
}
