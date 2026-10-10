using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Indexing;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Dashboard;

public sealed record DashboardCountsDto(int Conversations, int Projects, int Collections, int Documents, int ReadyDocuments,
    int FailedDocuments, int Chunks, int ActiveGenerations, int ActiveJobs, int FailedJobs, int StaleIndexes, int Files);
public sealed record RecentWorkDto(Guid Id, string Kind, string Title, DateTimeOffset UpdatedAt);
public sealed record DashboardDto(string Scope, DashboardCountsDto Counts, SpendReportDto Spend, IReadOnlyList<RecentWorkDto> Recent,
    bool WebSearchAvailable, bool GiteaAvailable, string EmbeddingMode, TokenUsageDto? Tokens = null);

/// <summary>Personal overview, or the platform-wide one for administrators (audited).</summary>
internal sealed class GetDashboard(NexusDbContext db, SpendReports reports, UsageReports usage, AccessService access, EmbeddingService embedding,
    AiNexus.Features.WebSearch.WebSearchService search, Microsoft.Extensions.Options.IOptions<AiNexus.Features.Repositories.GiteaOptions> gitea, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapGet("/dashboard", async (string? scope, Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offsetMinutes, ICurrentUser user, GetDashboard handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, scope ?? "personal", ownerId, from, until, offsetMinutes, ct)).ToHttpResult())
        .RequireAuthorization(Policies.Dashboard).WithName("GetDashboard").Produces<DashboardDto>();

    public async Task<Result<DashboardDto>> HandleAsync(Guid actor, string scope, Guid? ownerId, DateTimeOffset? from, DateTimeOffset? until, int? offset, CancellationToken ct)
    {
        if (scope is not ("personal" or "platform") || (scope == "personal" && ownerId != null)) return DashboardErrors.InvalidScope;
        if (scope == "platform" && !(await access.ForUserAsync(actor, ct)).Features.Any(x => x.Id == FeatureIds.Admin)) return DashboardErrors.AccessDenied;
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        Guid? owner = scope == "personal" ? actor : ownerId;
        var resources = db.Set<WorkspaceResource>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var docs = db.Set<KnowledgeDocument>().AsNoTracking().Where(d => !d.IsDeleted && resources.Any(x => x.Id == d.Id));
        var chunks = db.Set<KnowledgeChunk>().AsNoTracking().Where(x => docs.Any(d => d.Id == x.DocumentId));
        var jobs = db.Set<BackgroundJob>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        // One query per table: conditional counts share a scan. An empty table yields no group, which reads as zeros.
        var resourceCounts = await resources.Where(x => x.Kind == "project" || x.Kind == "knowledge").GroupBy(x => 1)
            .Select(g => new { Projects = g.Count(x => x.Kind == "project"), Collections = g.Count(x => x.Kind == "knowledge") }).SingleOrDefaultAsync(ct);
        var docCounts = await docs.GroupBy(x => 1).Select(g => new { All = g.Count(), Ready = g.Count(x => x.Status == "ready"), Failed = g.Count(x => x.Status == "failed"),
            Stale = g.Count(d => d.CollectionId != null && d.Status != "ready") }).SingleOrDefaultAsync(ct);
        var jobCounts = await jobs.Where(x => x.Status == "queued" || x.Status == "running" || x.Status == "failed").GroupBy(x => 1)
            .Select(g => new { Active = g.Count(x => x.Status == "queued" || x.Status == "running"), Failed = g.Count(x => x.Status == "failed") }).SingleOrDefaultAsync(ct);
        var counts = new DashboardCountsDto(
            await db.Conversations.CountAsync(x => owner == null || x.OwnerId == owner, ct),
            resourceCounts?.Projects ?? 0, resourceCounts?.Collections ?? 0,
            docCounts?.All ?? 0, docCounts?.Ready ?? 0, docCounts?.Failed ?? 0, await chunks.CountAsync(ct),
            await db.Runs.CountAsync(x => x.ActiveOwnerId != null && (owner == null || x.OwnerId == owner), ct),
            jobCounts?.Active ?? 0, jobCounts?.Failed ?? 0,
            embedding.Enabled ? docCounts?.Stale ?? 0 : 0,
            // Originals are counted once, independently of document readers and collection indexes.
            await db.Set<Attachment>().CountAsync(x => x.InLibrary && x.StorageState == AttachmentStates.Ready && (owner == null || x.OwnerId == owner), ct));
        var spend = await reports.ReportAsync(owner, period.Value, scope == "platform", ct);
        // Recent titles always belong to the current user, including in the platform scope.
        var recent = await db.Conversations.AsNoTracking().Where(x => x.OwnerId == actor)
            .OrderByDescending(x => x.UpdatedAt).Take(5).Select(x => new RecentWorkDto(x.Id, "chat", x.Title, x.UpdatedAt)).ToListAsync(ct);
        if (scope == "platform") { db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = ownerId ?? Guid.Empty, Action = "dashboard.platform.read", Result = "metrics" }); await db.SaveChangesAsync(ct); }
        return new DashboardDto(scope, counts, spend, recent, search.Status.Available, gitea.Value.Enabled, embedding.Enabled ? "語意向量" : "關鍵字",
            await usage.TokensAsync(owner, period.Value, scope == "platform", ct));
    }
}
