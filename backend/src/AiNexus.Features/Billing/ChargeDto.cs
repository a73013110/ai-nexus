namespace AiNexus.Features.Billing;

public sealed record ChargeDto(string State, string Kind, string Currency, decimal? Amount, long? InputTokens,
    long? CachedInputTokens, long? OutputTokens, long? ReasoningTokens);
