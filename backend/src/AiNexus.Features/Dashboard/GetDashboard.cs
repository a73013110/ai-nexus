using AiNexus.Features.Administration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Features.Operations;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Knowledge;
using Microsoft.EntityFrameworkCore;

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
        if (scope is not ("personal" or "platform") || (scope == "personal" && ownerId != null)) return Error.Invalid("invalid_dashboard_scope");
        if (scope == "platform" && !(await access.ForUserAsync(actor, ct)).Features.Any(x => x.Id == AdministrationConfiguration.Feature)) return Error.Forbidden("dashboard_access_denied");
        var period = SpendPeriod.Create(from, until, offset, clock.GetUtcNow());
        if (!period.IsSuccess) return period.Error;
        Guid? owner = scope == "personal" ? actor : ownerId;
        var resources = db.Set<WorkspaceResource>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var docs = db.Set<KnowledgeDocument>().AsNoTracking().Where(d => !d.IsDeleted && resources.Any(x => x.Id == d.Id));
        var chunks = db.Set<KnowledgeChunk>().AsNoTracking().Where(x => docs.Any(d => d.Id == x.DocumentId));
        var jobs = db.Set<BackgroundJob>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var counts = new DashboardCountsDto(
            await db.Conversations.CountAsync(x => owner == null || x.OwnerId == owner, ct),
            await resources.CountAsync(x => x.Kind == "project", ct), await resources.CountAsync(x => x.Kind == "knowledge", ct),
            await docs.CountAsync(ct), await docs.CountAsync(x => x.Status == "ready", ct), await docs.CountAsync(x => x.Status == "failed", ct), await chunks.CountAsync(ct),
            await db.Runs.CountAsync(x => x.ActiveOwnerId != null && (owner == null || x.OwnerId == owner), ct),
            await jobs.CountAsync(x => x.Status == "queued" || x.Status == "running", ct), await jobs.CountAsync(x => x.Status == "failed", ct),
            embedding.Enabled ? await docs.CountAsync(d => d.CollectionId != null && d.Status != "ready", ct) : 0,
            // Originals are counted once, independently of document readers and collection indexes.
            await db.Set<Attachment>().CountAsync(x => x.InLibrary && x.StorageState == AttachmentStates.Ready && (owner == null || x.OwnerId == owner), ct));
        var spend = await reports.ReportAsync(owner, period.Value, scope == "platform", ct);
        // Recent titles always belong to the current user, including in the platform scope.
        var recent = await db.Conversations.AsNoTracking().Where(x => x.OwnerId == actor)
            .OrderByDescending(x => x.UpdatedAt).Take(5).Select(x => new RecentWorkDto(x.Id, "chat", x.Title, x.UpdatedAt)).ToListAsync(ct);
        if (scope == "platform") { db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = ownerId ?? Guid.Empty, Action = "dashboard.platform.read", Result = "metrics" }); await db.SaveChangesAsync(ct); }
        return new DashboardDto(scope, counts, spend, recent, search.Status.Available, gitea.Value.Enabled, embedding.Enabled ? "語意向量" : "關鍵字",
            await usage.TokensAsync(owner, spend.From, spend.Until, spend.OffsetMinutes, scope == "platform", ct));
    }
}
