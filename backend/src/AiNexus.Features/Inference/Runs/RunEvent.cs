using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Inference.Runs;

/// <summary>
/// 生成事件實體。
/// 用於伺服器發送事件（SSE）斷線重連時的有序重播紀錄；完整最新狀態則以 <see cref="GenerationRun"/> 為準。
/// </summary>
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
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// RunEvent 的 EF Core 資料表與級聯刪除組態。
/// </summary>
internal sealed class RunEventConfiguration : IEntityTypeConfiguration<RunEvent>
{
    public void Configure(EntityTypeBuilder<RunEvent> runEvent)
    {
        runEvent.ToTable("RunEvents", "inference");
        // 複合主鍵：RunId + 遞增 Sequence
        runEvent.HasKey(x => new { x.RunId, x.Sequence });

        runEvent.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        runEvent.Property(x => x.Type).HasMaxLength(16);
        runEvent.Property(x => x.Status).HasMaxLength(16);
        runEvent.Property(x => x.IssueCode).HasMaxLength(40);
        runEvent.Property(x => x.ErrorCode).HasMaxLength(80);

        // 資料保留清理機制依據 CreatedAt 清理過期事件
        runEvent.HasIndex(x => x.CreatedAt);

        // 關聯 GenerationRun，當生成紀錄被刪除時級聯刪除關聯的所有事件
        runEvent.HasOne<GenerationRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
