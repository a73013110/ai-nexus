using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;

namespace AiNexus.Features.Account;

/// <summary>One user's last 30 days of model use and their attachment storage. Callers own authorization.</summary>
public sealed class PersonalUsage(UsageReports usage, AttachmentQuota quota)
{
    public async Task<PersonalUsageDto> ForOwnerAsync(Guid owner, CancellationToken ct)
    {
        var daily = await usage.DailyAsync(owner, ct);
        var totals = await usage.ByOwnersAsync([owner], ct) is [var mine, ..] ? mine.Usage : new(0, 0, 0, 0, 0, 0, 0);
        var storage = await quota.ForAsync(owner, ct);
        return new(30, totals.Requests, totals.Completed, totals.Failed, totals.Cancelled, totals.InputTokens, totals.OutputTokens, totals.RequestsWithUsage, daily, storage, totals.TotalDurationMilliseconds, totals.TimedRequests,
            await usage.TokensAsync(owner, UsageReports.Since, DateTimeOffset.UtcNow, 0, false, ct));
    }
}
