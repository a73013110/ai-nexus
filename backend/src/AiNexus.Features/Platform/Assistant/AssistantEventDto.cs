namespace AiNexus.Features.Platform.Assistant;

/// <summary>
/// 事實的種類，決定模型怎麼用它（提示詞以〈規則〉〈檢查〉標記）與畫面怎麼標示。
/// 「為什麼會出現、為什麼不見、為什麼是這個結果」這類問題，工具不必逐案寫回答規則：把判定與列出的條件寫成 <see cref="Rule"/>、
/// 對象的設定寫成 <see cref="Data"/>，模型就能組合說明；已知的設定問題再寫成 <see cref="Check"/>，附上處理頁。
/// </summary>
public enum AssistantFactKind
{
    /// <summary>查到的資料或對象的設定。</summary>
    Data,

    /// <summary>系統怎麼判定、資料怎麼列出的條文，與畫面上的說明同一份。</summary>
    Rule,

    /// <summary>系統比對設定後發現的問題，條件由工具算好；通常附 <see cref="AssistantLink"/> 指向處理頁。</summary>
    Check,
}

/// <summary>處理頁：<paramref name="Feature"/> 是 <c>FeatureCatalog</c> 的頁面 ID，<paramref name="Query"/> 是接在網址後的查詢字串（不含 <c>?</c>），與帳號待辦的連結同一種形狀。</summary>
public sealed record AssistantLink(string Feature, string? Query = null);

/// <summary>助理可以引用的一筆事實：<paramref name="Label"/> 是對象（「王小明 11/03（二）」「遲到」），<paramref name="Text"/> 是系統算好的內容。</summary>
public sealed record AssistantFact(string Label, string Text, AssistantFactKind Kind = AssistantFactKind.Data, AssistantLink? Link = null)
{
    public static AssistantFact Rule(string label, string text) => new(label, text, AssistantFactKind.Rule);

    public static AssistantFact Check(string label, string text, AssistantLink? link = null) => new(label, text, AssistantFactKind.Check, link);
}

/// <summary>
/// 畫面上的一筆依據（<see cref="AssistantFact"/> 送到前端的樣子）。<paramref name="Page"/> 是處理頁的名稱；
/// <paramref name="Url"/> 只在提問者持有那一頁時才有值（沒有權限的人只看到該找哪一頁處理）。
/// </summary>
public sealed record AssistantFactDto(string Label, string Text, AssistantFactKind Kind, string? Page = null, string? Url = null);

/// <summary>
/// 提問端點的 SSE 事件內容（<c>text/event-stream</c>），事件名稱決定哪些欄位有值：
/// <c>tool</c> 帶 <see cref="Tool"/>（開始執行的工具，畫面顯示正在查什麼）；
/// <c>grounding</c> 在每份資料交給模型時送一次，帶 <see cref="Scope"/>、這次新增的 <see cref="Facts"/>（編號接續前面的，第一份從 1 開始）與 <see cref="Partial"/>（放不下、只給了一部分）；
/// <c>delta</c> 帶回答的一段 <see cref="Text"/>；<c>done</c> 帶 <see cref="Truncated"/>（回答到了輸出上限被截斷）。失敗時是平台共用的 <c>error</c> 事件。
/// </summary>
public sealed record AssistantEventDto(
    string? Text = null,
    string? Tool = null,
    string? Scope = null,
    IReadOnlyList<AssistantFactDto>? Facts = null,
    bool? Partial = null,
    bool? Truncated = null);
