namespace AiNexus.Platform.Data.Tables;

/// <summary>
/// 列表端點共用的表格查詢參數（ADR 0026），以 <c>[AsParameters]</c> 綁在查詢字串；欄位 ID 由端點的 <see cref="TableColumns{T}"/> 宣告。
/// <list type="bullet">
/// <item><c>sort</c>：<c>欄位</c> 或 <c>欄位:desc</c>；省略時用端點的預設排序。</item>
/// <item><c>filter</c>：可重複，<c>欄位:運算:值</c>。運算為 <c>has</c>（文字包含）、<c>in</c>（選項，以逗號分隔）、<c>ge</c>、<c>le</c>、<c>lt</c>（範圍；日期的迄日由前端換成隔天 0 時並用 <c>lt</c>）。</item>
/// <item><c>offset</c>、<c>limit</c>：分頁；資料量在端點門檻內時忽略，一次回傳全部（<see cref="TablePage{T}.Complete"/>）。</item>
/// </list>
/// 用屬性而不是建構子參數：Request Delegate Generator 對 <c>[AsParameters]</c> 建構子上可為 null 的參考型別會產生無法編譯的程式碼。
/// </summary>
public sealed record TableRequest
{
    public int? Offset { get; init; }
    public int? Limit { get; init; }
    public string? Sort { get; init; }
    public string[]? Filter { get; init; }
}
