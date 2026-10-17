namespace AiNexus.Platform.Errors;

/// <summary>
/// 外部相依系統（模型供應商、目錄服務、Git 主機、外部資料庫等）故障或拒絕存取，而呼叫點無法回傳 <see cref="Result"/> 時
/// （例如串流內部、HTTP 用戶端底層或能力探針）才拋出此例外。可預期的使用案例失敗一律回傳 <see cref="Error"/>，不得拋出。
/// <see cref="Exception.Message"/> 只供日誌與維運指令查證，對外回應只包含 <see cref="Error"/>。
/// </summary>
public sealed class ExternalServiceException(Error error, string? detail = null, Exception? inner = null)
    : Exception(detail ?? error.Code, inner)
{
    public Error Error { get; } = error;
}
