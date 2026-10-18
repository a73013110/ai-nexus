using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.AccessControl;

/// <summary>功能群組的模型白名單、各模型每日 token 上限與附件空間。多個群組的授權採聯集。</summary>
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
        policy.ToTable("GroupModelPolicies", "accesscontrol");
        policy.HasKey(x => x.GroupId);
        policy.Property(x => x.GroupId).HasMaxLength(64);
        policy.Property(x => x.AllowedModelsJson).HasMaxLength(4000);
        policy.Property(x => x.DailyTokenLimitsJson).HasMaxLength(8000);
        policy.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
