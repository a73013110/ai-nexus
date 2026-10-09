using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

/// <summary>A group's model whitelist, daily token caps and attachment capacity. Groups grant additively.</summary>
public sealed class GroupModelPolicy
{
    public string GroupId { get; set; } = "";
    public string? AllowedModelsJson { get; set; }
    public string? DailyTokenLimitsJson { get; set; }
    public long? StoredAttachmentLimitBytes { get; set; }
}

internal sealed class GroupModelPolicyConfiguration : IEntityTypeConfiguration<GroupModelPolicy>
{
    public void Configure(EntityTypeBuilder<GroupModelPolicy> policy)
    {
        policy.ToTable("GroupModelPolicies", "access"); policy.HasKey(x => x.GroupId);
        policy.Property(x => x.GroupId).HasMaxLength(64); policy.Property(x => x.AllowedModelsJson).HasMaxLength(4000); policy.Property(x => x.DailyTokenLimitsJson).HasMaxLength(8000);
        policy.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
