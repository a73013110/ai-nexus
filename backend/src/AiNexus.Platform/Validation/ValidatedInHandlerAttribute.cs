namespace AiNexus.Platform.Validation;

/// <summary>
/// 標記刻意把驗證留在 Handler 內進行、而不用 <see cref="RequestValidator{T}"/> 的請求本體類別。
/// 常見原因：404 或 403 要先於 400 格式驗證、驗證失敗要在交易內寫入稽核紀錄，或要讓格式錯誤的回應與其他失敗一致。
/// <c>EndpointConventionTests</c> 要求每個請求模型都必須有 RequestValidator 或這個標記其中之一。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValidatedInHandlerAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
