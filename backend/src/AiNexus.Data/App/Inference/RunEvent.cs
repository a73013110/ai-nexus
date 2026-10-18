using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Inference;

/// <summary>
/// 生成事件實體。
/// 用於伺服器發送事件（SSE）斷線重連時的有序重播紀錄；完整最新狀態則以 <see cref="GenerationRun"/> 為準。
/// </summary>
public sealed class RunEvent
{
    public Guid RunId { get; set; }

    public long Sequence { get; set; }

    public string Type { get; set; } = "status";

    public string Status { get; set; } = RunStates.Queued;

    public string? Delta { get; set; }

    public string? IssueCode { get; set; }

    public string? ErrorCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class RunEventConfiguration : IEntityTypeConfiguration<RunEvent>
{
    public void Configure(EntityTypeBuilder<RunEvent> runEvent)
    {
        runEvent.ToTable("RunEvents", "inference");
        runEvent.HasKey(x => new { x.RunId, x.Sequence });
        runEvent.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        runEvent.Property(x => x.Type).HasMaxLength(16);
        runEvent.Property(x => x.Status).HasMaxLength(16);
        runEvent.Property(x => x.IssueCode).HasMaxLength(40);
        runEvent.Property(x => x.ErrorCode).HasMaxLength(80);
        runEvent.HasOne<GenerationRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
