namespace AiNexus.Features.Billing;

public sealed record PriceDto(Guid Id, string Provider, string ModelId, string Currency, string Kind,
    decimal InputPerMillion, decimal CachedInputPerMillion, decimal OutputPerMillion, decimal PerRequest,
    string RequestCharge, DateTimeOffset EffectiveAt, string Note, string? ModelDisplayName = null);
