namespace AiNexus.Features.Platform.Assistant;

/// <summary>助理可以引用的一筆事實：<paramref name="Label"/> 是對象（「王小明 11/03（二）」「規則：遲到」），<paramref name="Text"/> 是系統算好的內容。</summary>
public sealed record AssistantFact(string Label, string Text);

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
    IReadOnlyList<AssistantFact>? Facts = null,
    bool? Partial = null,
    bool? Truncated = null);
