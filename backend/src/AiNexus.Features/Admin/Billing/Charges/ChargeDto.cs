namespace AiNexus.Features.Admin.Billing.Charges;

/// <summary>單筆呼叫的費用與用量，未知的數值保持 null。</summary>
public sealed record ChargeDto(string State, string Kind, string Currency, decimal? Amount, long? InputTokens,
    long? CachedInputTokens, long? OutputTokens, long? ReasoningTokens);
