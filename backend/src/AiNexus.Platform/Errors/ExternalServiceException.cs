namespace AiNexus.Platform.Errors;

/// <summary>
/// 當外部相依系統（模型供應商、目錄服務、Git 主機、外部資料庫等）發生故障或拒絕存取，
/// 且執行點無法回傳 <see cref="Result"/> 時（例如串流內部、HTTP 客戶端底層或能力探針中）拋出此例外狀況。
/// 使用案例（use case）可預期的失敗一律回傳 <see cref="Error"/> 純值，不得拋出此例外。
/// <see cref="Exception.Message"/> 僅供日誌與維運指令查證，對外公開回應僅包含 <see cref="Error"/>。
/// </summary>
public sealed class ExternalServiceException(Error error, string? detail = null, Exception? inner = null)
    : Exception(detail ?? error.Code, inner)
{
    public Error Error { get; } = error;
}
