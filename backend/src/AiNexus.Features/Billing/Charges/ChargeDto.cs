namespace AiNexus.Features.Billing.Charges;

public sealed record ChargeDto(string State, string Kind, string Currency, decimal? Amount, long? InputTokens,
    long? CachedInputTokens, long? OutputTokens, long? ReasoningTokens);
