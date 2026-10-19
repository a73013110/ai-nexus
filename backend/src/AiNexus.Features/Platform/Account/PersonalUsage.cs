using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Admin.Billing.Reports;

namespace AiNexus.Features.Platform.Account;

public sealed record PersonalUsageDto(int Days, int Requests, int Completed, int Failed, int Cancelled,
    long InputTokens, long OutputTokens, int RequestsWithUsage,
    IReadOnlyList<UsageDayDto> Daily, AttachmentStorageDto Storage, long TotalDurationMilliseconds = 0, int TimedRequests = 0, TokenUsageDto? Tokens = null);

/// <summary>單一使用者最近 30 天的模型用量與附件儲存量；權限檢查由呼叫端負責。</summary>
public sealed class PersonalUsage(UsageReports usage, AttachmentQuota quota, TimeProvider clock)
{
    public async Task<PersonalUsageDto> ForOwnerAsync(Guid owner, CancellationToken ct)
    {
        var daily = await usage.DailyAsync(owner, ct);
        var totals = await usage.ByOwnersAsync([owner], ct) is [var mine, ..] ? mine.Usage : new(0, 0, 0, 0, 0, 0, 0);
        var storage = await quota.ForAsync(owner, ct);
        return new(30, totals.Requests, totals.Completed, totals.Failed, totals.Cancelled, totals.InputTokens, totals.OutputTokens, totals.RequestsWithUsage, daily, storage, totals.TotalDurationMilliseconds, totals.TimedRequests,
            await usage.TokensAsync(owner, new SpendPeriod(usage.Since, clock.GetUtcNow(), 0), false, ct));
    }
}
