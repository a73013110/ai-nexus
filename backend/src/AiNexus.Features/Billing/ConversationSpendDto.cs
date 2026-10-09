namespace AiNexus.Features.Billing;

public sealed record ConversationSpendDto(int Requests, int PendingCalls, int LegacyCalls, IReadOnlyList<MoneyTotalDto> Totals,
    IReadOnlyList<SpendBucketDto> Models);
