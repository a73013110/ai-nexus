using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.WebSearch;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Operations;

public sealed record AdminUsageDto(int Users, int Requests, int Completed, long InputTokens, long OutputTokens, int RequestsWithUsage, long TotalDurationMilliseconds, int TimedRequests,
    int ActiveUsers, int Failed, int Cancelled, long StoredBytes, int StoredFiles, IReadOnlyList<UsageModelDto> Models, IReadOnlyList<UsageKindDto> Kinds,
    IReadOnlyList<ProviderStatusDto> Providers, WebSearchStatusDto WebSearch, DateTimeOffset Since, DateTimeOffset Until);

/// <summary>自 <see cref="UsageReports.Since"/> 起的平台用量、儲存空間、模型供應商健康度與網路搜尋狀態。</summary>
internal sealed class GetAdminUsage(AppDbContext db, UsageReports reports, ModelCatalog catalog, WebSearchService search, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/usage", async (GetAdminUsage handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
        .WithName("GetAdminUsage");

    public async Task<AdminUsageDto> HandleAsync(CancellationToken ct)
    {
        var totals = await reports.AllAsync(ct);
        var models = await reports.ModelsAsync(ct);
        var providers = await catalog.ProviderStatusesAsync(ct);
        var users = await db.Users.CountAsync(x => x.Enabled && x.DeletedAt == null, ct);
        var activeUsers = await reports.ActiveOwnersAsync(ct);
        var storedBytes = await db.Set<Attachment>().SumAsync(x => x.Size, ct);
        var storedFiles = await db.Set<Attachment>().CountAsync(ct);
        var kinds = await reports.PlatformKindsAsync(ct);
        return new(users, totals.Requests, totals.Completed, totals.InputTokens, totals.OutputTokens, totals.RequestsWithUsage, totals.TotalDurationMilliseconds, totals.TimedRequests,
            activeUsers, totals.Failed, totals.Cancelled, storedBytes, storedFiles,
            models, kinds, providers, search.Status, reports.Since, clock.GetUtcNow());
    }
}
