using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Quality;

public sealed class MessageFeedback
{
    public Guid MessageId { get; set; }
    public Guid OwnerId { get; set; }
    public int Rating { get; set; }
    public string Reason { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class EvaluationSet
{
    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public string CasesJson { get; set; } = "[]";
    public int Version { get; set; } = 1;
}
public sealed class EvaluationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SetId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid JobId { get; set; }
    public int SetVersion { get; set; }
    public string SetTitle { get; set; } = "";
    public string CasesJson { get; set; } = "[]";
    public string VariantsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class EvaluationResult
{
    public Guid RunId { get; set; }
    public int CaseIndex { get; set; }
    public int VariantIndex { get; set; }
    public string Output { get; set; } = "";
    public bool Truncated { get; set; }
    public int RequiredMatches { get; set; }
    public int RequiredTotal { get; set; }
    public int ForbiddenMatches { get; set; }
    public long ElapsedMs { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public int? ReviewScore { get; set; }
    public string ReviewNote { get; set; } = "";
    public Guid? ReviewerId { get; set; }
}
public sealed record FeedbackRequest(int Rating, string Reason = "", string Note = "");
public sealed record FeedbackDto(Guid MessageId, Guid ConversationId, string ConversationTitle, int Rating, string Reason, string Note, DateTimeOffset UpdatedAt);
public sealed record EvaluationCase(string Question, string Reference = "", IReadOnlyList<string>? RequiredTerms = null, IReadOnlyList<string>? ForbiddenTerms = null);
public sealed record EvaluationSetRequest(string Name, string Description, IReadOnlyList<EvaluationCase> Cases, int ExpectedVersion = 1);
public sealed record EvaluationSetDto(ResourceDto Resource, string Description, IReadOnlyList<EvaluationCase> Cases, int Version);
public sealed record EvaluationVariantRequest(string Label, string? ModelId = null, string Instruction = "");
public sealed record EvaluationVariant(string Label, string ModelId, string Instruction, ModelTaskSnapshot? Configuration = null, string? ModelDisplayName = null);
public sealed record EvaluationRunRequest(IReadOnlyList<EvaluationVariantRequest> Variants);
public sealed record EvaluationRunDto(Guid Id, Guid SetId, string Title, int SetVersion, bool CanControl, DateTimeOffset CreatedAt, JobDto Job);
public sealed record EvaluationResultDto(int CaseIndex, int VariantIndex, string Output, bool Truncated, int RequiredMatches, int RequiredTotal, int ForbiddenMatches, long ElapsedMs, long? InputTokens, long? OutputTokens, int? ReviewScore, string ReviewNote);
public sealed record EvaluationDetailDto(EvaluationRunDto Run, IReadOnlyList<EvaluationCase> Cases, IReadOnlyList<EvaluationVariant> Variants, IReadOnlyList<EvaluationResultDto> Results, bool CanReview);
public sealed record ReviewRequest(int? Score, string Note = "");

public static class QualityConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        RetrievalEvaluationConfiguration.Configure(model);
        var f = model.Entity<MessageFeedback>(); f.ToTable("MessageFeedback", "quality"); f.HasKey(x => x.MessageId);
        f.Property(x => x.Reason).HasMaxLength(24); f.Property(x => x.Note).HasMaxLength(2000); f.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
        f.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        f.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var s = model.Entity<EvaluationSet>(); s.ToTable("EvaluationSets", "quality"); s.HasKey(x => x.Id); s.Property(x => x.Description).HasMaxLength(2000);
        s.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        var r = model.Entity<EvaluationRun>(); r.ToTable("EvaluationRuns", "quality"); r.HasKey(x => x.Id); r.Property(x => x.SetTitle).HasMaxLength(120); r.HasIndex(x => new { x.SetId, x.CreatedAt }); r.HasIndex(x => x.JobId).IsUnique();
        r.HasOne<EvaluationSet>().WithMany().HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Restrict);
        r.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        r.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        var v = model.Entity<EvaluationResult>(); v.ToTable("EvaluationResults", "quality"); v.HasKey(x => new { x.RunId, x.CaseIndex, x.VariantIndex }); v.Property(x => x.ReviewNote).HasMaxLength(2000);
        v.HasOne<EvaluationRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        v.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
    }
}
