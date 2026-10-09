using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Inference;

[Comment("生成事件的有序 SSE 重播紀錄；完整生成狀態以 GenerationRuns 為準。")]
public sealed class RunEvent
{
    [Comment("關聯 GenerationRuns 的識別碼。")]
    public Guid RunId { get; set; }
    [Comment("事件在同一生成中的遞增序號。")]
    public long Sequence { get; set; }
    [Comment("事件的種類。")]
    public string Type { get; set; } = "status";
    [Comment("業務執行狀態。")]
    public string Status { get; set; } = RunStates.Queued;
    [Comment("生成文字增量或完整快照。")]
    public string? Delta { get; set; }
    [Comment("伺服器產生的不透明問題查證代碼；每個問題個別識別。")]
    public string? IssueCode { get; set; }
    [Comment("對外安全的錯誤代碼，不含密碼或完整例外。")]
    public string? ErrorCode { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class RunEventConfiguration : IEntityTypeConfiguration<RunEvent>
{
    public void Configure(EntityTypeBuilder<RunEvent> runEvent)
    {
        runEvent.ToTable("RunEvents", "inference");
        runEvent.HasKey(x => new { x.RunId, x.Sequence });
        runEvent.Property(x => x.Type).HasMaxLength(16);
        runEvent.Property(x => x.Status).HasMaxLength(16);
        runEvent.Property(x => x.IssueCode).HasMaxLength(40);
        runEvent.Property(x => x.ErrorCode).HasMaxLength(80);
        runEvent.HasOne<GenerationRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
