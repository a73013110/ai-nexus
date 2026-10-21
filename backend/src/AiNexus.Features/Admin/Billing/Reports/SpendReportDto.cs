namespace AiNexus.Features.Admin.Billing.Reports;

// MoneyTotalDto、SpendBucketDto（每日）與 SpendUserDto 由 billing.usp_GetSpendReport 的結果集依建構子直接對應：
// 改名、改順序或改型別要同時改 procedure 的 SELECT，否則只在執行時失敗。
public sealed record MoneyTotalDto(string Currency, string Kind, decimal Amount, int KnownCalls, int UnknownCalls);

public sealed record SpendBucketDto(string Label, string Currency, string Kind, decimal Amount, int Requests,
    int UnknownCalls, long InputTokens, long OutputTokens);

public sealed record SpendUserDto(Guid OwnerId, string DisplayName, string Account, string Currency, string Kind,
    decimal Amount, int Requests, int UnknownCalls);

public sealed record SpendReportDto(DateTimeOffset From, DateTimeOffset Until, int OffsetMinutes, int Requests,
    int PendingCalls, int LegacyCalls, long InputTokens, long OutputTokens, IReadOnlyList<MoneyTotalDto> Totals,
    IReadOnlyList<SpendBucketDto> Daily, IReadOnlyList<SpendBucketDto> Models, IReadOnlyList<SpendUserDto> Users);
