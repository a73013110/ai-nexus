using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Flow;

/// <summary>申請送出時快照的一關與它的決定；改流程不影響進行中的申請。</summary>
public sealed class FlowInstanceStep
{
    public const int CommentMaxLength = 500;

    public Guid InstanceId { get; set; }
    public int StepNumber { get; set; }
    public string Name { get; set; } = "";
    public string ApproverRule { get; set; } = "";
    public string? ApproverArgument { get; set; }

    /// <summary><see cref="FlowStepStatuses"/> 之一。</summary>
    public string Status { get; set; } = FlowStepStatuses.Waiting;

    /// <summary>略過的原因，<see cref="FlowStepNotes"/> 之一。</summary>
    public string? Note { get; set; }

    /// <summary>規則算不出審核人，改由簽核管理員審核。</summary>
    public bool Fallback { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DeciderId { get; set; }
    public bool OnBehalf { get; set; }
    public string? Comment { get; set; }
}

public static class FlowStepStatuses
{
    public const string Waiting = "waiting";
    public const string Active = "active";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Skipped = "skipped";

    /// <summary>申請在這一關之前或這一關結束（退回、取消、逾期），這關沒有走完。</summary>
    public const string Stopped = "stopped";
}

public static class FlowStepNotes
{
    /// <summary>關卡的條件不成立。</summary>
    public const string NotApplicable = "not-applicable";

    /// <summary>候選人中有人已核准本申請前面的關卡。</summary>
    public const string AlreadyApproved = "already-approved";
}

internal sealed class FlowInstanceStepConfiguration : IEntityTypeConfiguration<FlowInstanceStep>
{
    public void Configure(EntityTypeBuilder<FlowInstanceStep> step)
    {
        step.ToTable("FlowInstanceSteps", "flow");
        step.HasKey(x => new { x.InstanceId, x.StepNumber });
        step.Property(x => x.Name).HasMaxLength(FlowDefinitionStep.NameMaxLength);
        step.Property(x => x.ApproverRule).HasMaxLength(FlowDefinitionStep.RuleMaxLength);
        step.Property(x => x.ApproverArgument).HasMaxLength(FlowDefinitionStep.ArgumentMaxLength);
        step.Property(x => x.Status).HasMaxLength(16);
        step.Property(x => x.Note).HasMaxLength(32);
        step.Property(x => x.Comment).HasMaxLength(FlowInstanceStep.CommentMaxLength);
        step.HasOne<FlowInstance>().WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Restrict);
        step.HasOne<User>().WithMany().HasForeignKey(x => x.DeciderId).OnDelete(DeleteBehavior.Restrict);
    }
}
