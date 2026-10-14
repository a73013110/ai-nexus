namespace AiNexus.Features.Billing.Reports;

public sealed record ConversationSpendDto(int Requests, int PendingCalls, int LegacyCalls, IReadOnlyList<MoneyTotalDto> Totals,
    IReadOnlyList<SpendBucketDto> Models);
