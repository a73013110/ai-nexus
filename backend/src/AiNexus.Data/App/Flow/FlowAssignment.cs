using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Flow;

/// <summary>
/// 審核中那一關的候選審核人：關卡啟動與排程重算時整批改寫，「待我審核」只是這張表的索引查詢。
/// 它是快取；決定當下會再即時算一次，不在名單內就拒絕（ADR 0036）。
/// </summary>
public sealed class FlowAssignment
{
    public Guid InstanceId { get; set; }
    public Guid UserId { get; set; }
}

internal sealed class FlowAssignmentConfiguration : IEntityTypeConfiguration<FlowAssignment>
{
    public void Configure(EntityTypeBuilder<FlowAssignment> assignment)
    {
        assignment.ToTable("FlowAssignments", "flow");
        assignment.HasKey(x => new { x.InstanceId, x.UserId });
        assignment.HasOne<FlowInstance>().WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Restrict);
        assignment.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
