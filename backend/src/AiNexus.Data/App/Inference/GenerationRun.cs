using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Inference;

/// <summary>
/// 聊天生成任務實體。
/// 記錄單次對話生成的生命週期狀態、冪等請求識別、執行租約、回答快照與 Token 用量。
/// </summary>
public sealed class GenerationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerId { get; set; }

    public string? TraceId { get; set; }

    public string? ParentSpanId { get; set; }

    public Guid OperationId { get; set; }

    /// <summary>
    /// 當前仍在執行的擁有者。
    /// 資料庫的 filtered unique index 限制同一擁有者同時只能有一個執行中的生成（不為 null 的列）。
    /// </summary>
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

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public long ReservedTokens { get; set; }

    public long? InputTokens { get; set; }

    public long? OutputTokens { get; set; }

    public long? DurationMilliseconds { get; set; }

    public long? GenerationMilliseconds { get; set; }
}

internal sealed class GenerationRunConfiguration : IEntityTypeConfiguration<GenerationRun>
{
    public void Configure(EntityTypeBuilder<GenerationRun> run)
    {
        run.ToTable("GenerationRuns", "inference");
        run.HasKey(x => x.Id);
        run.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        run.Property(x => x.ModelId).HasMaxLength(160);
        run.Property(x => x.Provider).HasMaxLength(32);
        run.Property(x => x.ProviderModelId).HasMaxLength(150);
        run.Property(x => x.Status).HasMaxLength(16);
        run.Property(x => x.IssueCode).HasMaxLength(40);
        run.Property(x => x.TraceId).HasMaxLength(32);
        run.Property(x => x.ParentSpanId).HasMaxLength(16);
        run.Property(x => x.ErrorCode).HasMaxLength(80);
        run.Property(x => x.IdempotencyKey).HasMaxLength(80);
        run.Property(x => x.RequestHash).HasMaxLength(64);
        run.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<Message>().WithMany().HasForeignKey(x => x.UserMessageId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<Message>().WithMany().HasForeignKey(x => x.AssistantMessageId).OnDelete(DeleteBehavior.Restrict);
    }
}
