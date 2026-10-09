namespace AiNexus.Features.Billing;

public sealed record MoneyTotalDto(string Currency, string Kind, decimal Amount, int KnownCalls, int UnknownCalls);

public sealed record SpendBucketDto(string Label, string Currency, string Kind, decimal Amount, int Requests,
    int UnknownCalls, long InputTokens, long OutputTokens);

public sealed record SpendUserDto(Guid OwnerId, string DisplayName, string Account, string Currency, string Kind,
    decimal Amount, int Requests, int UnknownCalls);

public sealed record SpendReportDto(DateTimeOffset From, DateTimeOffset Until, int OffsetMinutes, int Requests,
    int PendingCalls, int LegacyCalls, long InputTokens, long OutputTokens, IReadOnlyList<MoneyTotalDto> Totals,
    IReadOnlyList<SpendBucketDto> Daily, IReadOnlyList<SpendBucketDto> Models, IReadOnlyList<SpendUserDto> Users);
