namespace AiNexus.Platform.Auditing;

/// <summary>端點宣告：不寫稽核，並說明原因（選項查詢、輪詢、另有專屬稽核等）；<c>RequestAuditTests</c> 要求每個 API 端點二選一。</summary>
public sealed record UnauditedEndpoint(string Reason);
