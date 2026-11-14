using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Flow;

/// <summary>
/// 一次送出的申請（流程實例）。簽核狀態只存在這裡，業務模組的表只存申請內容並以 <see cref="SubjectId"/> 對應；
/// 標題與 <see cref="Details"/> 是送出時的快照，通用畫面不必認得每種申請。
/// </summary>
public sealed class FlowInstance : IRowVersioned
{
    public const int TitleMaxLength = 200;
    public const int DetailsMaxLength = 4000;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "";
    public Guid SubjectId { get; set; }
    public Guid ApplicantId { get; set; }
    public string Title { get; set; } = "";

    /// <summary>JSON 陣列 <c>[{label, value}]</c>。</summary>
    public string Details { get; set; } = "[]";

    /// <summary><see cref="FlowStatuses"/> 之一。</summary>
    public string Status { get; set; } = FlowStatuses.Pending;

    /// <summary>審核中的關卡序號；只有 <see cref="FlowStatuses.Pending"/> 有值（<c>ck_FlowInstances_CurrentStep</c>）。</summary>
    public int? CurrentStep { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public long RowVersion { get; set; }
}

public static class FlowStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
}

internal sealed class FlowInstanceConfiguration : IEntityTypeConfiguration<FlowInstance>
{
    public void Configure(EntityTypeBuilder<FlowInstance> instance)
    {
        instance.ToTable("FlowInstances", "flow");
        instance.HasKey(x => x.Id);
        instance.Property(x => x.Kind).HasMaxLength(FlowDefinition.KindMaxLength);
        instance.Property(x => x.Title).HasMaxLength(FlowInstance.TitleMaxLength);
        instance.Property(x => x.Details).HasMaxLength(FlowInstance.DetailsMaxLength);
        instance.Property(x => x.Status).HasMaxLength(16);
        instance.Property(x => x.RowVersion).IsRowVersion().HasConversion<byte[]>();
        instance.HasOne<FlowDefinition>().WithMany().HasForeignKey(x => x.Kind).OnDelete(DeleteBehavior.Restrict);
        instance.HasOne<User>().WithMany().HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.Restrict);
    }
}
