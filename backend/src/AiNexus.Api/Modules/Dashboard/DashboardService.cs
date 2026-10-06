using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Billing;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Knowledge;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Dashboard;

public sealed record DashboardCountsDto(int Conversations, int Projects, int Collections, int Documents, int ReadyDocuments,
    int FailedDocuments, int Chunks, int ActiveGenerations, int ActiveJobs, int FailedJobs, int StaleIndexes, int Files);
public sealed record RecentWorkDto(Guid Id, string Kind, string Title, DateTimeOffset UpdatedAt);
public sealed record DashboardDto(string Scope, DashboardCountsDto Counts, SpendReportDto Spend, IReadOnlyList<RecentWorkDto> Recent,
    bool WebSearchAvailable, bool GiteaAvailable, string EmbeddingMode);

public sealed class DashboardService(NexusDbContext db, SpendReports reports, AccessService access, IEmbeddingProvider embedding,
    AiNexus.Modules.WebSearch.WebSearchService search, Microsoft.Extensions.Options.IOptions<AiNexus.Modules.Repositories.GiteaOptions> gitea)
{
    public async Task<DashboardDto> GetAsync(Guid actor, string scope, Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offset, CancellationToken ct)
    {
        if (scope is not ("personal" or "platform") || (scope == "personal" && ownerId != null)) throw new ApiException(400, "invalid_dashboard_scope", "檢視範圍不正確。");
        if (scope == "platform" && !(await access.ForUserAsync(actor, ct)).Features.Any(x => x.Id == "admin")) throw new ApiException(403, "dashboard_access_denied", "只有管理員可檢視平台總覽。");
        Guid? owner = scope == "personal" ? actor : ownerId;
        var resources = db.Set<WorkspaceResource>().AsNoTracking().Where(x => !x.IsDeleted && (owner == null || x.OwnerId == owner));
        var docs = db.Set<KnowledgeDocument>().AsNoTracking().Where(d => !d.IsDeleted && resources.Any(x => x.Id == d.Id));
        var chunks = db.Set<KnowledgeChunk>().AsNoTracking().Where(x => docs.Any(d => d.Id == x.DocumentId));
        var jobs = db.Set<BackgroundJob>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var counts = new DashboardCountsDto(
            await db.Conversations.CountAsync(x => !x.IsDeleted && (owner == null || x.OwnerId == owner), ct),
            await resources.CountAsync(x => x.Kind == "project", ct), await resources.CountAsync(x => x.Kind == "knowledge", ct),
            await docs.CountAsync(ct), await docs.CountAsync(x => x.Status == "ready", ct), await docs.CountAsync(x => x.Status == "failed", ct), await chunks.CountAsync(ct),
            await db.Runs.CountAsync(x => x.ActiveOwnerId != null && (owner == null || x.OwnerId == owner), ct),
            await jobs.CountAsync(x => x.Status == "queued" || x.Status == "running", ct), await jobs.CountAsync(x => x.Status == "failed", ct),
            embedding.Enabled ? await docs.CountAsync(d => d.Status == "ready" && db.Set<KnowledgeChunk>().Any(x => x.DocumentId == d.Id && x.EmbeddingProfile != embedding.Profile), ct) : 0,
            // Originals are counted once, independently of document readers and collection indexes.
            await db.Set<Attachment>().CountAsync(x => x.InLibrary && x.StorageState == AttachmentStates.Ready && (owner == null || x.OwnerId == owner), ct));
        var spend = await reports.ReportAsync(owner, from, until, offset, scope == "platform", ct);
        // Recent titles always belong to the current user, including in the platform scope.
        var recent = await db.Conversations.AsNoTracking().Where(x => x.OwnerId == actor && !x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt).Take(5).Select(x => new RecentWorkDto(x.Id, "chat", x.Title, x.UpdatedAt)).ToListAsync(ct);
        if (scope == "platform") { db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = ownerId ?? Guid.Empty, Action = "dashboard.platform.read", Result = "metrics" }); await db.SaveChangesAsync(ct); }
        return new(scope, counts, spend, recent, search.Status.Available, gitea.Value.Enabled, embedding.Enabled ? "語意向量" : "關鍵字");
    }
}
