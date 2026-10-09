using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Administration;

/// <summary>A group's model whitelist, daily token caps and attachment capacity. Groups grant additively.</summary>
public sealed class GroupModelPolicy
{
    public string GroupId { get; set; } = "";
    public string? AllowedModelsJson { get; set; }
    public string? DailyTokenLimitsJson { get; set; }
    public long? StoredAttachmentLimitBytes { get; set; }
}

/// <summary>A user's personal whitelist (narrows group grants) and token cap overrides.</summary>
public sealed class UserModelPolicy
{
    public Guid UserId { get; set; }
    public string? AllowedModelsJson { get; set; }
    public string? DailyTokenLimitsJson { get; set; }
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

internal sealed class UserModelPolicyConfiguration : IEntityTypeConfiguration<UserModelPolicy>
{
    public void Configure(EntityTypeBuilder<UserModelPolicy> personal)
    {
        personal.ToTable("UserModelPolicies", "access"); personal.HasKey(x => x.UserId);
        personal.Property(x => x.AllowedModelsJson).HasMaxLength(4000); personal.Property(x => x.DailyTokenLimitsJson).HasMaxLength(8000);
    }
}
