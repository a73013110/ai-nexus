namespace AiNexus.Platform.Auditing;

/// <summary>端點的稽核宣告（ADR 0023）：每個 API 端點擇一。</summary>
public static class AuditEndpointExtensions
{
    /// <summary>每次請求寫一筆 <paramref name="action"/>；端點上的宣告優先於群組上的。</summary>
    public static TBuilder Audited<TBuilder>(this TBuilder builder, AuditAction action) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new AuditedEndpoint(action));

    /// <summary>不寫稽核；<paramref name="reason"/> 寫給讀程式的人，說明為什麼這不是需要留證的存取或異動。</summary>
    public static TBuilder NotAudited<TBuilder>(this TBuilder builder, string reason) where TBuilder : IEndpointConventionBuilder
        => string.IsNullOrWhiteSpace(reason) ? throw new ArgumentException("必須說明不稽核的原因。", nameof(reason)) : builder.WithMetadata(new UnauditedEndpoint(reason));

    /// <summary>端點最後宣告的稽核方式（端點層覆寫群組層）；沒有宣告時為 null。</summary>
    public static AuditAction? AuditActionOf(this Endpoint? endpoint) => endpoint is null ? null : Resolve(endpoint.Metadata);

    /// <summary>建置端點時讀的同一個宣告，讓過濾器在啟動時就決定要不要掛上。</summary>
    public static AuditAction? AuditActionOf(this EndpointBuilder builder) => Resolve(builder.Metadata);

    // metadata 依群組到端點的順序排列，取最後一筆宣告。
    private static AuditAction? Resolve(IEnumerable<object> metadata)
        => metadata.LastOrDefault(item => item is AuditedEndpoint or UnauditedEndpoint) is AuditedEndpoint audited ? audited.Action : null;
}
