using AiNexus.Modules.Identity;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Quality;

public sealed class RetrievalEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid JobId { get; set; }
    public string Title { get; set; } = "";
    public string CollectionsJson { get; set; } = "[]";
    public string CasesJson { get; set; } = "[]";
    public string ConfigurationFingerprint { get; set; } = "";
    public string ProfileKey { get; set; } = "";
    public int TopK { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class RetrievalEvaluationResult
{
    public Guid RunId { get; set; }
    public int CaseIndex { get; set; }
    public string Mode { get; set; } = "";
    public string ActualMode { get; set; } = "";
    public string? Unavailable { get; set; }
    public double? Recall { get; set; }
    public double? ReciprocalRank { get; set; }
    public double? Ndcg { get; set; }
    public bool? Refused { get; set; }
    public long RewriteMs { get; set; }
    public long EmbedMs { get; set; }
    public long SearchMs { get; set; }
    public long RerankMs { get; set; }
    public long ElapsedMs { get; set; }
}
public sealed record RetrievalRelevance(Guid DocumentId, IReadOnlyList<int> Pages, int Grade = 1);
public sealed record RetrievalEvaluationCase(string Id, string Query, IReadOnlyList<RetrievalRelevance> Relevant, bool NoAnswer = false);
public sealed record RetrievalEvaluationRequest(string Title, IReadOnlyList<Guid> CollectionIds, IReadOnlyList<RetrievalEvaluationCase> Cases);
public sealed record RetrievalEvaluationDto(Guid Id, string Title, int Cases, int TopK, string ProfileKey, string ConfigurationFingerprint, DateTimeOffset CreatedAt, JobDto Job);
public sealed record RetrievalMetricDto(string CaseId, string Mode, string ActualMode, string? Unavailable, double? Recall, double? ReciprocalRank, double? Ndcg, bool? Refused, long RewriteMs, long EmbedMs, long SearchMs, long RerankMs, long ElapsedMs);
public sealed record RetrievalLatencyDto(double P50, double P95);
public sealed record RetrievalSummaryDto(string Mode, bool Comparable, int Completed, int Unavailable, double? Recall, double? Mrr, double? Ndcg, double? RefusalRate, RetrievalLatencyDto Rewrite, RetrievalLatencyDto Embed, RetrievalLatencyDto Search, RetrievalLatencyDto Rerank, RetrievalLatencyDto Total);
public sealed record RetrievalReportDto(RetrievalEvaluationDto Run, IReadOnlyList<RetrievalSummaryDto> Summary, IReadOnlyList<RetrievalMetricDto> Results, string RefusalDefinition = "無答案題未提供任何來源的比例；不評斷生成回答內容。", string LatencyDefinition = "依 vector、keyword、hybrid、hybrid+rerank 順序執行；共用正常查詢快取，延遲包含快取命中。相鄰片段合併後依相關文件及頁碼評分。");

public static class RetrievalEvaluationConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var r = model.Entity<RetrievalEvaluation>(); r.ToTable("RetrievalEvaluations", "quality"); r.HasKey(x => x.Id);
        r.Property(x => x.Title).HasMaxLength(120); r.Property(x => x.ProfileKey).HasMaxLength(200); r.Property(x => x.ConfigurationFingerprint).HasMaxLength(64);
        r.HasIndex(x => new { x.OwnerId, x.CreatedAt }); r.HasIndex(x => x.JobId).IsUnique();
        r.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        r.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        var v = model.Entity<RetrievalEvaluationResult>(); v.ToTable("RetrievalEvaluationResults", "quality"); v.HasKey(x => new { x.RunId, x.CaseIndex, x.Mode });
        v.Property(x => x.Mode).HasMaxLength(24); v.Property(x => x.ActualMode).HasMaxLength(120); v.Property(x => x.Unavailable).HasMaxLength(80);
        v.HasOne<RetrievalEvaluation>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
