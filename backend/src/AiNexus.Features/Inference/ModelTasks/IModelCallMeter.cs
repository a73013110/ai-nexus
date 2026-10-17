namespace AiNexus.Features.Inference.ModelTasks;

/// <summary>
/// 模型呼叫計費與用量計量介面。
/// 由計費（Billing）模組實作，解耦推論模組對計費具體實作的直接相依。
/// 每個呼叫僅追蹤實體變更，由呼叫端統一儲存變更。
/// </summary>
public interface IModelCallMeter
{
    /// <summary>依據建立時間 <paramref name="created"/> 當下生效的費率預約扣款與計量。</summary>
    Task ReserveAsync(Guid callId, Guid owner, Guid? conversation, string provider, string model, string operation, DateTimeOffset created, CancellationToken ct);

    /// <summary>標記模型呼叫開始進入執行階段。</summary>
    Task StartAsync(Guid callId, CancellationToken ct);

    /// <summary>計量即時串流回傳的 Token 用量。</summary>
    Task MeterAsync(Guid callId, long? input, long? output, long? cached, long? reasoning, CancellationToken ct, bool complete = false);

    /// <summary>完成呼叫計費並結算最終結果狀態。</summary>
    Task FinishAsync(Guid callId, string outcome, CancellationToken ct);
}
