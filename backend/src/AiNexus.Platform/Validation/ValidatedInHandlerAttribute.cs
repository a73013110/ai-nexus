namespace AiNexus.Platform.Validation;

/// <summary>
/// 標註將驗證邏輯刻意保留在 Handler 內部進行、而非使用 <see cref="RequestValidator{T}"/> 的請求本體類別。
/// 常見原因包括：404 或 403 權限優先於 400 格式驗證、驗證失敗需在交易內寫入稽核記錄，或使特定格式錯誤外觀與其他失敗一致。
/// <c>EndpointConventionTests</c> 會嚴格要求所有請求模型必須具備 RequestValidator 或此標籤其一。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValidatedInHandlerAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
