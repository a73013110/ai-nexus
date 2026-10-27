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
    public static AuditAction? AuditActionOf(this Endpoint? endpoint)
    {
        var metadata = endpoint?.Metadata;
        if (metadata is null) return null;
        for (var i = metadata.Count - 1; i >= 0; i--)
        {
            if (metadata[i] is AuditedEndpoint audited) return audited.Action;
            if (metadata[i] is UnauditedEndpoint) return null;
        }
        return null;
    }
}
