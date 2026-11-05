namespace AiNexus.Features.Platform.Assistant;

/// <summary>助理可以引用的一筆事實：<paramref name="Label"/> 是對象（「王小明 11/03（二）」「規則：遲到」），<paramref name="Text"/> 是系統算好的內容。</summary>
public sealed record AssistantFact(string Label, string Text);

/// <summary>
/// 頁面交給助理的資料。<paramref name="Subject"/> 是頁面名稱（寫進系統指令），<paramref name="Scope"/> 是一行範圍說明（面板頂端顯示），
/// <paramref name="Guidance"/> 是這一頁特有的回答規則（名詞的意思、常見問題怎麼答）。
/// <paramref name="Facts"/> 依重要性排序：模型上下文放不下時從後面捨去，所以範圍、規則與彙總放前面，逐日明細放後面。
/// </summary>
public sealed record AssistantGrounding(string Subject, string Scope, string Guidance, IReadOnlyList<AssistantFact> Facts);

/// <summary>
/// 提問端點的 SSE 事件內容（<c>text/event-stream</c>），事件名稱決定哪些欄位有值：
/// <c>grounding</c> 在第一段文字之前送一次，帶 <see cref="Scope"/>、實際交給模型的 <see cref="Facts"/>（編號＝索引＋1）與 <see cref="Partial"/>（事實放不下、只給了一部分）；
/// <c>delta</c> 帶回答的一段 <see cref="Text"/>；<c>done</c> 帶 <see cref="Truncated"/>（回答到了輸出上限被截斷）。失敗時是平台共用的 <c>error</c> 事件。
/// </summary>
public sealed record AssistantEventDto(
    string? Text = null,
    string? Scope = null,
    IReadOnlyList<AssistantFact>? Facts = null,
    bool? Partial = null,
    bool? Truncated = null);
