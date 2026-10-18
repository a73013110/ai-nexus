using System.Text;

namespace AiNexus.Features.Platform.Inference.ModelTasks;

/// <summary>
/// 模型輸入訊息 Token 成本保守評估工具。
/// 計算 UTF-8 位元組數並加上訊息包裝（framing）開銷，圖片則依其解析度估算 Token。
/// 用於在發送推論請求前預先預估配額與驗證上下文邊界。
/// </summary>
public static class MessageCost
{
    /// <summary>估算訊息清單的整體 Token 消耗（含對話額外 framing 128 tokens）。</summary>
    public static long Estimate(IReadOnlyList<InferenceMessage> messages) => messages.Sum(Of) + 128;

    /// <summary>估算單則訊息（含文本內容與圖片）的 Token 消耗。</summary>
    public static long Of(InferenceMessage message) => Of(message.Content) + (message.Images ?? []).Sum(x => (long)x.EstimatedTokens);

    /// <summary>依據文字 UTF-8 位元組長度加 32 tokens framing 估算。</summary>
    public static int Of(string value) => Encoding.UTF8.GetByteCount(value) + 32;
}
