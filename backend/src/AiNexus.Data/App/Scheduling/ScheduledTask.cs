using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Scheduling;

/// <summary>
/// 一個排程任務的執行狀態。排程規則、名稱與說明在程式（<c>IScheduledTask</c>）與設定，這裡只存跨程序共用的狀態：
/// 下次執行、暫停、手動要求與租約。程式不認得的 <see cref="Key"/> 一律略過，開發機與正式站共用資料庫時新舊版本互不干擾。
/// </summary>
public sealed class ScheduledTask
{
    public const int KeyMaxLength = 100;
    public const int HostMaxLength = 100;
    public const int ParametersMaxLength = 1000;

    public string Key { get; set; } = "";
    public bool IsPaused { get; set; }
    public Guid? PausedById { get; set; }
    public DateTimeOffset? PausedAt { get; set; }

    /// <summary>null 表示不再依排程執行（啟動任務已成功）。</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    public DateTimeOffset? RunRequestedAt { get; set; }
    public Guid? RunRequestedById { get; set; }
    public string? RunParameters { get; set; }
    public bool CancelRequested { get; set; }

    /// <summary>租約三欄同時有值或同時為 null（<c>ck_ScheduledTasks_Lease</c>）。</summary>
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public string? LeaseHost { get; set; }

    public int ConsecutiveFailures { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class ScheduledTaskConfiguration : IEntityTypeConfiguration<ScheduledTask>
{
    public void Configure(EntityTypeBuilder<ScheduledTask> task)
    {
        task.ToTable("ScheduledTasks", "scheduling");
        task.HasKey(x => x.Key);
        task.Property(x => x.Key).HasMaxLength(ScheduledTask.KeyMaxLength);
        task.Property(x => x.RunParameters).HasMaxLength(ScheduledTask.ParametersMaxLength);
        task.Property(x => x.LeaseHost).HasMaxLength(ScheduledTask.HostMaxLength);
        task.HasOne<User>().WithMany().HasForeignKey(x => x.PausedById).OnDelete(DeleteBehavior.Restrict);
        task.HasOne<User>().WithMany().HasForeignKey(x => x.RunRequestedById).OnDelete(DeleteBehavior.Restrict);
    }
}
