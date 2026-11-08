using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Scheduling;

/// <summary>排程任務的一輪；開始時新增（running），結束時寫入結果。業務明細仍記在各模組自己的表。</summary>
public sealed class ScheduledTaskRun
{
    public const int StageMaxLength = 120;
    public const int SummaryMaxLength = 500;
    public const int ErrorCodeMaxLength = 80;
    public const int IssueCodeMaxLength = 40;
    public const int ErrorMessageMaxLength = 500;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string TaskKey { get; set; } = "";

    /// <summary><see cref="ScheduledTaskTriggers"/> 之一。</summary>
    public string TriggerKind { get; set; } = "";

    /// <summary>只有手動執行才有值。</summary>
    public Guid? RequestedById { get; set; }

    public string Host { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary><see cref="ScheduledTaskRunStatuses"/> 之一。</summary>
    public string Status { get; set; } = ScheduledTaskRunStatuses.Running;

    public string? Stage { get; set; }
    public int? CompletedUnits { get; set; }
    public int? TotalUnits { get; set; }
    public string? Summary { get; set; }
    public string? ErrorCode { get; set; }
    public string? IssueCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TraceId { get; set; }
}

public static class ScheduledTaskTriggers
{
    public const string Scheduled = "scheduled";
    public const string Manual = "manual";
    public const string Startup = "startup";
}

public static class ScheduledTaskRunStatuses
{
    public const string Running = "running";
    public const string Succeeded = "succeeded";

    /// <summary>執行完成但沒事可做；保存期較短，管理頁可隱藏。</summary>
    public const string Idle = "idle";

    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    /// <summary>程序關閉或中止；租約釋放後會再執行。</summary>
    public const string Interrupted = "interrupted";
}

internal sealed class ScheduledTaskRunConfiguration : IEntityTypeConfiguration<ScheduledTaskRun>
{
    public void Configure(EntityTypeBuilder<ScheduledTaskRun> run)
    {
        run.ToTable("ScheduledTaskRuns", "scheduling");
        run.HasKey(x => x.Id);
        run.Property(x => x.TaskKey).HasMaxLength(ScheduledTask.KeyMaxLength);
        run.Property(x => x.TriggerKind).HasMaxLength(16);
        run.Property(x => x.Host).HasMaxLength(ScheduledTask.HostMaxLength);
        run.Property(x => x.Status).HasMaxLength(16);
        run.Property(x => x.Stage).HasMaxLength(ScheduledTaskRun.StageMaxLength);
        run.Property(x => x.Summary).HasMaxLength(ScheduledTaskRun.SummaryMaxLength);
        run.Property(x => x.ErrorCode).HasMaxLength(ScheduledTaskRun.ErrorCodeMaxLength);
        run.Property(x => x.IssueCode).HasMaxLength(ScheduledTaskRun.IssueCodeMaxLength);
        run.Property(x => x.ErrorMessage).HasMaxLength(ScheduledTaskRun.ErrorMessageMaxLength);
        run.Property(x => x.TraceId).HasMaxLength(32);
        run.HasOne<ScheduledTask>().WithMany().HasForeignKey(x => x.TaskKey).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
    }
}
