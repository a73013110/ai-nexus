using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.WebSearches;
using AiNexus.Platform.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Operations;

public sealed record AdminUsageDto(int Users, int Requests, int Completed, long InputTokens, long OutputTokens, int RequestsWithUsage, long TotalDurationMilliseconds, int TimedRequests,
    int ActiveUsers, int Failed, int Cancelled, long StoredBytes, int StoredFiles, IReadOnlyList<UsageModelDto> Models, IReadOnlyList<UsageKindDto> Kinds,
    IReadOnlyList<ProviderStatusDto> Providers, WebSearchStatusDto WebSearch, DateTimeOffset Since, DateTimeOffset Until);

/// <summary>自 <see cref="UsageReports.Since"/> 起的平台用量、儲存空間、模型供應商健康度與網路搜尋狀態。</summary>
internal sealed class GetAdminUsage(AppDbContext db, IActiveUsers activeUsers, UsageReports reports, ModelCatalog catalog, WebSearchService search, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/usage", (GetAdminUsage handler, CancellationToken ct) =>
            handler.HandleAsync(ct))
        .Audited(OperationsAudit.UsageRead)
        .WithName("GetAdminUsage");

    public async Task<AdminUsageDto> HandleAsync(CancellationToken ct)
    {
        var totals = await reports.AllAsync(ct);
        var models = await reports.ModelsAsync(ct);
        var providers = await catalog.ProviderStatusesAsync(ct);
        var users = await activeUsers.Ids.CountAsync(ct);
        var activeOwners = await reports.ActiveOwnersAsync(ct);
        // 與儲存空間監控同一口徑：只計已寫入完成的原檔，上傳中與刪除中的列不算。
        var stored = await db.Attachments.AsNoTracking().Where(x => x.StorageState == AttachmentStates.Ready).GroupBy(x => 1)
            .Select(g => new { Files = g.Count(), Bytes = g.Sum(x => x.Size) }).SingleOrDefaultAsync(ct);
        var kinds = await reports.PlatformKindsAsync(ct);
        return new(users, totals.Requests, totals.Completed, totals.InputTokens, totals.OutputTokens, totals.RequestsWithUsage, totals.TotalDurationMilliseconds, totals.TimedRequests,
            activeOwners, totals.Failed, totals.Cancelled, stored?.Bytes ?? 0, stored?.Files ?? 0,
            models, kinds, providers, search.Status, reports.Since, clock.GetUtcNow());
    }
}
