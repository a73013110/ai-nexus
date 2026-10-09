using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Features.WebSearch;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

public sealed record AdminUsageDto(int Users, int Requests, int Completed, long InputTokens, long OutputTokens, int RequestsWithUsage, long TotalDurationMilliseconds, int TimedRequests,
    int ActiveUsers, int Failed, int Cancelled, long StoredBytes, int StoredFiles, IReadOnlyList<UsageModelDto> Models, IReadOnlyList<UsageKindDto> Kinds,
    IReadOnlyList<ProviderStatusDto> Providers, WebSearchStatusDto WebSearch, DateTimeOffset Since, DateTimeOffset Until);

/// <summary>Platform usage since <see cref="UsageReports.Since"/>, storage, provider health and web search status.</summary>
internal sealed class GetAdminUsage(NexusDbContext db, UsageReports reports, ModelCatalog catalog, WebSearchService search, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/usage", async (GetAdminUsage handler, CancellationToken ct) => Results.Ok(await handler.HandleAsync(ct)))
        .WithName("GetAdminUsage").Produces<AdminUsageDto>();

    public async Task<AdminUsageDto> HandleAsync(CancellationToken ct)
    {
        var totals = await reports.AllAsync(ct);
        var models = await reports.ModelsAsync(ct);
        var providers = await catalog.ProviderStatusesAsync(ct);
        return new(await db.Users.CountAsync(x => x.Enabled && x.DeletedAt == null, ct), totals.Requests, totals.Completed, totals.InputTokens, totals.OutputTokens, totals.RequestsWithUsage, totals.TotalDurationMilliseconds, totals.TimedRequests,
            await reports.ActiveOwnersAsync(ct), totals.Failed, totals.Cancelled, await db.Set<Attachment>().SumAsync(x => x.Size, ct), await db.Set<Attachment>().CountAsync(ct),
            models, await reports.PlatformKindsAsync(ct), providers, search.Status, UsageReports.Since, clock.GetUtcNow());
    }
}
