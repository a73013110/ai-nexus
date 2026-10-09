using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>One comparison of model variants over a frozen copy of a set's cases, executed by <see cref="EvaluationHandler"/>.</summary>
[Comment("評測執行的題庫與模型設定快照、背景工作與狀態。")]
public sealed class EvaluationRun
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("評測題庫識別碼。")]
    public Guid SetId { get; set; }
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("關聯背景工作識別碼。")]
    public Guid JobId { get; set; }
    [Comment("評測執行當時的題庫版本。")]
    public int SetVersion { get; set; }
    [Comment("評測執行當時的題庫名稱快照。")]
    public string SetTitle { get; set; } = "";
    [Comment("固定評測案例的 JSON 快照。")]
    public string CasesJson { get; set; } = "[]";
    [Comment("評測模型／參數組合的 JSON 快照。")]
    public string VariantsJson { get; set; } = "[]";
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }

    public EvaluationRunDto ToDto(BackgroundJob job, Guid actor) => new(Id, SetId, SetTitle, SetVersion, OwnerId == actor, CreatedAt, JobService.Describe(job));
}

internal sealed class EvaluationRunConfiguration : IEntityTypeConfiguration<EvaluationRun>
{
    public void Configure(EntityTypeBuilder<EvaluationRun> r)
    {
        r.ToTable("EvaluationRuns", "quality"); r.HasKey(x => x.Id); r.Property(x => x.SetTitle).HasMaxLength(120); r.HasIndex(x => new { x.SetId, x.CreatedAt }); r.HasIndex(x => x.JobId).IsUnique();
        r.HasOne<EvaluationSet>().WithMany().HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Restrict);
    }
}
