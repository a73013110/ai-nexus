using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Inference;

/// <summary>One chat answer being generated.</summary>
public sealed class GenerationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string? TraceId { get; set; }
    public string? ParentSpanId { get; set; }
    public Guid OperationId { get; set; }
    // Unique filtered index enforces one active generation per owner, even across requests.
    public Guid? ActiveOwnerId { get; set; }
    public Guid? ExecutorId { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserMessageId { get; set; }
    public Guid AssistantMessageId { get; set; }
    public string ModelId { get; set; } = "";
    public string Provider { get; set; } = "google";
    public string ProviderModelId { get; set; } = "";
    public string ParametersJson { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Status { get; set; } = RunStates.Queued;
    public string Content { get; set; } = "";
    public long LastSequence { get; set; }
    public string? IssueCode { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long ReservedTokens { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? DurationMilliseconds { get; set; }
    public long? GenerationMilliseconds { get; set; }
}

/// <remarks>Foreign keys to conversations, messages and users are in <c>CrossModuleRelationships</c>.</remarks>
internal sealed class GenerationRunConfiguration : IEntityTypeConfiguration<GenerationRun>
{
    public void Configure(EntityTypeBuilder<GenerationRun> run)
    {
        run.ToTable("GenerationRuns", "inference");
        run.HasKey(x => x.Id);
        run.Property(x => x.ModelId).HasMaxLength(160);
        run.Property(x => x.Provider).HasMaxLength(32);
        run.Property(x => x.ProviderModelId).HasMaxLength(150);
        run.Property(x => x.Status).HasMaxLength(16);
        run.Property(x => x.IssueCode).HasMaxLength(40); run.Property(x => x.TraceId).HasMaxLength(32); run.Property(x => x.ParentSpanId).HasMaxLength(16);
        run.Property(x => x.ErrorCode).HasMaxLength(80);
        run.Property(x => x.IdempotencyKey).HasMaxLength(80);
        run.Property(x => x.RequestHash).HasMaxLength(64);
        run.HasIndex(x => new { x.OwnerId, x.IdempotencyKey }).IsUnique();
        run.HasIndex(x => x.ActiveOwnerId).IsUnique().HasFilter("[ActiveOwnerId] IS NOT NULL");
        run.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        run.HasIndex(x => new { x.OwnerId, x.CreatedAt }); // Daily token budgets and personal usage reports.
        run.HasIndex(x => new { x.ActiveOwnerId, x.LeaseExpiresAt });
    }
}
