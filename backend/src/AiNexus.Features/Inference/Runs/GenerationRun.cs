using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Inference.Runs;

/// <summary>
/// 聊天生成任務實體。
/// 記錄單次對話生成的生命週期狀態、冪等請求識別、執行租約、回答快照與 Token 用量。
/// </summary>
[Comment("聊天生成的持久狀態、冪等請求、執行租約、回答及用量。")]
public sealed class GenerationRun
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }

    [Comment("W3C 流程追蹤識別，僅由伺服器建立。")]
    public string? TraceId { get; set; }

    [Comment("排程來源的 W3C span 識別，重試沿用同一 trace。")]
    public string? ParentSpanId { get; set; }

    [Comment("持久作業識別，跨佇列與重試保持不變。")]
    public Guid OperationId { get; set; }

    /// <summary>
    /// 當前仍在執行的擁有者。
    /// 【關鍵併發防禦】：透過 Filtered Unique Index 限制同一擁有者同一時間僅能有一個執行中的生成（ActiveOwnerId 不為 null）。
    /// </summary>
    [Comment("仍在執行的擁有者；filtered unique index 限制每人一個生成。")]
    public Guid? ActiveOwnerId { get; set; }

    [Comment("處理此次生成的伺服器程序識別碼。")]
    public Guid? ExecutorId { get; set; }

    [Comment("生成 executor 租約的到期時間。")]
    public DateTimeOffset? LeaseExpiresAt { get; set; }

    [Comment("關聯對話的識別碼。")]
    public Guid ConversationId { get; set; }

    [Comment("此次生成的使用者提問訊息識別碼。")]
    public Guid UserMessageId { get; set; }

    [Comment("此次生成的 AI 回答訊息識別碼。")]
    public Guid AssistantMessageId { get; set; }

    [Comment("核准模型的內部識別碼。")]
    public string ModelId { get; set; } = "";

    [Comment("模型或搜尋服務供應商識別碼。")]
    public string Provider { get; set; } = "google";

    [Comment("送往指定供應商的原生模型識別碼，與核准路由識別碼分開保存。")]
    public string ProviderModelId { get; set; } = "";

    [Comment("執行參數的 JSON 快照，不含服務密鑰。")]
    public string ParametersJson { get; set; } = "{}";

    [Comment("擁有者範圍內的冪等請求識別，避免重試重複處理。")]
    public string IdempotencyKey { get; set; } = "";

    [Comment("請求內容指紋，用於辨識冪等識別碼衝突。")]
    public string RequestHash { get; set; } = "";

    [Comment("業務執行狀態。")]
    public string Status { get; set; } = RunStates.Queued;

    [Comment("生成中或已完成的回答文字快照。")]
    public string Content { get; set; } = "";

    [Comment("最後已持久化的生成事件序號。")]
    public long LastSequence { get; set; }

    [Comment("伺服器產生的不透明問題查證代碼；每個問題個別識別。")]
    public string? IssueCode { get; set; }

    [Comment("對外安全的錯誤代碼，不含密碼或完整例外。")]
    public string? ErrorCode { get; set; }

    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }

    [Comment("工作開始執行時間。")]
    public DateTimeOffset? StartedAt { get; set; }

    [Comment("工作結束時間。")]
    public DateTimeOffset? FinishedAt { get; set; }

    [Comment("生成預留的保守輸入加最大輸出 token；執行中或缺失 usage 時占用配額，未執行即取消釋放。")]
    public long ReservedTokens { get; set; }

    [Comment("模型回報的輸入 tokens；未知保持空值。")]
    public long? InputTokens { get; set; }

    [Comment("模型回報的輸出 tokens；未知保持空值。")]
    public long? OutputTokens { get; set; }

    [Comment("從請求建立至終止的總耗時毫秒；包括排隊、生成、取消與失敗。")]
    public long? DurationMilliseconds { get; set; }

    [Comment("從生成開始至終止的耗時毫秒；未開始的請求保持空值。")]
    public long? GenerationMilliseconds { get; set; }
}

/// <summary>
/// GenerationRun 的 EF Core 資料表結構與防禦性索引組態。
/// </summary>
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

        // 冪等唯一索引：同一使用者不可重送相同 IdempotencyKey
        run.HasIndex(x => new { x.OwnerId, x.IdempotencyKey }).IsUnique();

        // 單人單次活躍生成唯一過濾索引：限制同一使用者同時僅能有一個活躍生成（ActiveOwnerId 不為 NULL）
        run.HasIndex(x => x.ActiveOwnerId)
            .IsUnique()
            .HasFilter("[ActiveOwnerId] IS NOT NULL");

        run.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        // 每日 Token 預算與個人用量報表查詢索引
        run.HasIndex(x => new { x.OwnerId, x.CreatedAt });

        // 全平台日期範圍查詢索引（管理員用量報表、監控指標統計），包含必要欄位以避免 Bookmark Lookup
        run.HasIndex(x => x.CreatedAt).IncludeProperties(x => new
        {
            x.OwnerId,
            x.ModelId,
            x.Status,
            x.InputTokens,
            x.OutputTokens,
            x.DurationMilliseconds
        });

        // 租約超時恢復掃描索引
        run.HasIndex(x => new { x.ActiveOwnerId, x.LeaseExpiresAt });
    }
}
