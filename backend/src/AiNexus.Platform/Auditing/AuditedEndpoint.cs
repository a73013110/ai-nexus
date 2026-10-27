namespace AiNexus.Platform.Auditing;

/// <summary>端點宣告：每次請求寫一筆 <see cref="Action"/> 稽核（成功或失敗都寫）。</summary>
public sealed record AuditedEndpoint(AuditAction Action);
